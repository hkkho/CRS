-- Accept daily-turn save v2 and retain legacy v1. Replacing the existing
-- function preserves its ownership, grants, RLS and concurrency contracts.
create or replace function public.save_player_run(payload jsonb, expected_revision integer)
returns integer language plpgsql security definer set search_path = '' as $$
declare
  uid uuid := auth.uid();
  rid uuid;
  existing public.player_runs;
  next_revision integer;
begin
  if uid is null then raise exception 'Sign in to save.'; end if;
  if jsonb_typeof(payload) is distinct from 'object'
    or coalesce(payload->>'version', '') not in ('1', '2')
    or jsonb_typeof(payload->'commands') is distinct from 'array'
    or jsonb_typeof(payload->'ended') is distinct from 'boolean'
    or coalesce(payload->>'shiftId', '') not in ('free-practice', 'useful-fuel-day-v1')
    or coalesce(length(payload->>'scorePolicyId'), 0) not between 1 and 200
    or coalesce(length(payload->>'dataPackId'), 0) not between 1 and 200
    or coalesce(length(payload->>'stateDigest'), 0) not between 1 and 256
    or octet_length(payload::text) > 12000000
    then raise exception 'Invalid save format.'; end if;
  if payload->>'version' = '2' and (
    coalesce(payload->>'pacingMode', '') not in ('daily-turn', 'real-time') or
    jsonb_typeof(payload->'draftChannels') is distinct from 'array') then
    raise exception 'Invalid daily save format.';
  end if;
  if payload->>'version' = '2' and jsonb_array_length(payload->'draftChannels') > 380 then
    raise exception 'Invalid daily fuel plan.';
  end if;
  if jsonb_array_length(payload->'commands') not between 1 and 100000
    or not coalesce((payload->>'score')::numeric between 0 and 1000000000, false)
    or not coalesce((payload->>'seconds')::numeric between 0 and 3600000000000, false)
    or not coalesce((payload->>'fuelConsumed')::numeric between 0 and 1000000000, false)
    then raise exception 'Invalid save values.'; end if;
  rid := (payload->>'id')::uuid;
  if rid is null then raise exception 'Missing run ID.'; end if;
  -- Serialize even the first insert, so two devices cannot race to create a run.
  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(uid::text || rid::text, 0));
  select * into existing from public.player_runs where user_id = uid and id = rid for update;
  if coalesce(existing.revision, 0) is distinct from expected_revision then
    raise exception 'Cloud save changed on another device. Refresh saves and continue the cloud copy.';
  end if;
  if existing.run->>'ended' = 'true' and payload->>'ended' <> 'true' then
    raise exception 'Ended runs cannot be continued.';
  end if;
  if (payload->>'seconds')::numeric < (existing.run->>'seconds')::numeric then
    raise exception 'This save is older than the cloud copy. Continue the cloud copy.';
  end if;
  if existing.run is not null and (
    coalesce(payload->>'pacingMode', 'real-time') is distinct from coalesce(existing.run->>'pacingMode', 'real-time') or
    payload->>'seed' is distinct from existing.run->>'seed' or
    payload->>'shiftId' is distinct from existing.run->>'shiftId' or
    payload->>'dataPackId' is distinct from existing.run->>'dataPackId' or
    payload->>'scorePolicyId' is distinct from existing.run->>'scorePolicyId') then
    raise exception 'A run cannot change its seed, objective, simulation or scoring policy.';
  end if;
  next_revision := coalesce(existing.revision, 0) + 1;
  insert into public.player_runs(user_id, id, revision, run) values (uid, rid, next_revision, payload)
  on conflict (user_id, id) do update set revision = next_revision, run = payload, updated_at = now();
  return next_revision;
end $$;
