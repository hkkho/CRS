-- Apply with the Supabase SQL editor or `supabase db push`.
-- Private saves and public, explicitly published, self-reported endless scores.
create table public.player_runs (
  user_id uuid not null references auth.users(id) on delete cascade,
  id uuid not null,
  revision integer not null default 1,
  run jsonb not null check (octet_length(run::text) <= 12000000),
  updated_at timestamptz not null default now(),
  primary key (user_id, id)
);
alter table public.player_runs enable row level security;
revoke all on public.player_runs from anon, authenticated;
grant select on public.player_runs to authenticated;
create policy "Read own runs" on public.player_runs for select to authenticated
  using ((select auth.uid()) = user_id);

create table public.endless_scores (
  user_id uuid not null references auth.users(id) on delete cascade,
  policy text not null,
  display_name text not null,
  score double precision not null,
  seconds double precision not null,
  published_at timestamptz not null default now(),
  primary key (user_id, policy)
);
alter table public.endless_scores enable row level security;
revoke all on public.endless_scores from anon, authenticated;

create function public.save_player_run(payload jsonb, expected_revision integer)
returns integer language plpgsql security definer set search_path = '' as $$
declare
  uid uuid := auth.uid();
  rid uuid;
  existing public.player_runs;
  next_revision integer;
begin
  if uid is null then raise exception 'Sign in to save.'; end if;
  if jsonb_typeof(payload) is distinct from 'object'
    or payload->>'version' is distinct from '1'
    or jsonb_typeof(payload->'commands') is distinct from 'array'
    or jsonb_typeof(payload->'ended') is distinct from 'boolean'
    or coalesce(payload->>'shiftId', '') not in ('free-practice', 'useful-fuel-day-v1')
    or coalesce(length(payload->>'scorePolicyId'), 0) not between 1 and 200
    or coalesce(length(payload->>'dataPackId'), 0) not between 1 and 200
    or coalesce(length(payload->>'stateDigest'), 0) not between 1 and 256
    or octet_length(payload::text) > 12000000
    then raise exception 'Invalid save format.'; end if;
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

create function public.publish_endless_score(run_id uuid)
returns void language plpgsql security definer set search_path = '' as $$
declare
  uid uuid := auth.uid();
  saved jsonb;
  github_name text;
begin
  if uid is null then raise exception 'Sign in to publish a score.'; end if;
  select run into saved from public.player_runs where user_id = uid and id = run_id;
  if saved is null or saved->>'ended' <> 'true' or saved->>'shiftId' <> 'free-practice' then
    raise exception 'Only ended endless runs can be published.';
  end if;
  -- Identity comes from the provider, never a client-supplied display name.
  select coalesce(identity_data->>'user_name', identity_data->>'preferred_username', 'GitHub player')
    into github_name from auth.identities where user_id = uid and provider = 'github' limit 1;
  if github_name is null then raise exception 'GitHub login is required.'; end if;
  insert into public.endless_scores(user_id, policy, display_name, score, seconds)
    values (uid, saved->>'scorePolicyId', left(github_name, 100), (saved->>'score')::double precision, (saved->>'seconds')::double precision)
  on conflict (user_id, policy) do update set display_name = excluded.display_name,
    score = excluded.score, seconds = excluded.seconds, published_at = now()
    where excluded.score > public.endless_scores.score;
end $$;

create function public.endless_leaderboard(policy text)
returns table(display_name text, score double precision, seconds double precision)
language sql stable security definer set search_path = '' as $$
  select s.display_name, s.score, s.seconds from public.endless_scores s
    where s.policy = $1 order by s.score desc, s.published_at asc, s.user_id asc limit 50;
$$;

revoke all on function public.save_player_run(jsonb, integer) from public, anon;
revoke all on function public.publish_endless_score(uuid) from public, anon;
revoke all on function public.endless_leaderboard(text) from public;
grant execute on function public.save_player_run(jsonb, integer) to authenticated;
grant execute on function public.publish_endless_score(uuid) to authenticated;
grant execute on function public.endless_leaderboard(text) to anon, authenticated;
