// Small original play experiments. References identify design studies, not reused assets.
export const patterns = [
  {
    id: "council",
    title: "Cloud Council",
    genre: "Requests & consequences",
    source: "Sort the Court!",
    url: "https://graebor.itch.io/sort-the-court",
    accent: "#d07542",
    background: "#d7e8e5",
    premise: "Keep a floating town together through eight competing requests.",
    lesson:
      "A tiny input vocabulary can support substantial decisions when costs, reactions and future needs are readable.",
    art: "Rounded original residents, terracotta roofs, paper requests and a cloud-borne settlement.",
    motion:
      "Visitor arrival → decision → reaction → resource change → next request.",
    test: "Can a player explain why accepting every request eventually creates a problem?",
  },
  {
    id: "post",
    title: "Moonpost Desk",
    genre: "Tactile work",
    source: "Peggy's Post",
    url: "https://digitarium.itch.io/peggys-post",
    accent: "#a35c36",
    background: "#e9d9b6",
    premise:
      "Read a parcel, choose exact postage, and send it to the right route.",
    lesson:
      "Work becomes play when distinct tools, object states and a short completion ritual make mastery tangible.",
    art: "Original kraft-paper parcels, indigo routing trays, an ink pad and a wooden workbench.",
    motion: "Pick up → drag with a shadow → preview route → release → receipt.",
    test: "Can someone send a parcel without memorizing an unexplained icon or tiny number?",
  },
  {
    id: "water",
    title: "Water District",
    genre: "Networks & scarce capacity",
    source: "Waterworks!",
    url: "https://scriptwelder.itch.io/waterworks",
    accent: "#38768b",
    background: "#dce7d7",
    premise:
      "Connect three homes to a well, then increase capacity before ending the day.",
    lesson:
      "A spatial dependency becomes understandable when its functioning and disconnected consumers are visible.",
    art: "An original top-down district with cottage silhouettes, brass pipe joints and blue flow markers.",
    motion:
      "Cell preview → pipe placement → connected-path pulse → house acknowledgement.",
    test: "Can a player distinguish a missing connection from insufficient pump capacity?",
  },
  {
    id: "harvest",
    title: "Night Harvest",
    genre: "Manual work → automation",
    source: "1,000,000 shrimp",
    url: "https://jontopielski.itch.io/1000000-shrimp",
    accent: "#e5b769",
    background: "#192d37",
    premise:
      "Gather luminous sea seeds and invest in a net and a stronger vessel.",
    lesson:
      "Let the player experience manual work before buying a visible tool that changes the production rate.",
    art: "An original two-tone fishing scene with a low moon, rigged vessel, ripples and luminous catches.",
    motion: "Net arc → catch burst → cargo count → a larger working net.",
    test: "Does an upgrade feel different in the scene as well as on the counter?",
  },
  {
    id: "flock",
    title: "Meadow Flock",
    genre: "Care & collection",
    source: "Peckin' Pixels",
    url: "https://wavingwalrus.itch.io/peckin-pixels",
    accent: "#b87636",
    background: "#e1e8c6",
    premise:
      "Feed an original little flock, collect eggs, and grow a healthy coop.",
    lesson:
      "Readable needs and individual variations can make production feel like caring for living things.",
    art: "Original hens with distinct warm plumage, straw nests, fenced grass and a small red-roofed coop.",
    motion: "Walk and peck → feed scatter → nest pulse → egg collection.",
    test: "Does the player notice an animal’s need before production stops?",
  },
  {
    id: "factory",
    title: "Copper Works",
    genre: "Production & bottlenecks",
    source: "The MachinEGG",
    url: "https://quantumgames-studio.itch.io/the-machinegg",
    accent: "#bd694a",
    background: "#e6dfd2",
    premise:
      "Turn ore into parts, sell output, and automate without filling the buffer.",
    lesson:
      "An upgrade has a purpose when it removes an understood bottleneck and exposes the next one.",
    art: "An original copper press, moving belts, packed crates, gauges and striped industrial supports.",
    motion: "Feedstock → press stroke → conveyor transfer → output stack.",
    test: "Can a player explain why a full output buffer stops the machine?",
  },
  {
    id: "garden",
    title: "Window Garden",
    genre: "Calm growth",
    source: "plant daddy",
    url: "https://overfull.itch.io/plantdaddy",
    accent: "#57835e",
    background: "#e7e8d3",
    premise:
      "Water a sunny shelf, sell mature plants, and make room for new pots.",
    lesson:
      "The visible object can be the reward; not every simulation needs a threatening countdown.",
    art: "Original terracotta and glazed pots, varied leaf shapes, a wooden shelf and soft window light.",
    motion: "Water droplets → damp soil → new stem and leaf → a fuller shelf.",
    test: "Can the player see growth and distinguish mature from thirsty plants?",
  },
  {
    id: "evolution",
    title: "Creature Trials",
    genre: "Experiment & compare",
    source: "Evolution",
    url: "https://keiwan.itch.io/evolution",
    accent: "#7a70a0",
    background: "#e8e5ed",
    premise: "Run six toy gaits, retain the best, and mutate a new generation.",
    lesson:
      "Comparing a visible experiment against a previous best can create curiosity without prizes.",
    art: "Original jointed mechanical creatures on six lanes with a shared distance scale.",
    motion:
      "Articulated gait → moving distance markers → highlighted best → next trial.",
    test: "Can someone connect a changed parameter to a changed result? This is a deterministic toy optimizer, not a neural-network or rigid-body reproduction.",
  },
  {
    id: "orb",
    title: "Lumen Workshop",
    genre: "Conversions & capacity",
    source: "Orb of Creation",
    url: "https://marple.itch.io/orb-of-creation",
    accent: "#7a66ad",
    background: "#20263c",
    premise:
      "Convert charge into light and material, then expand capacity to awaken a world.",
    lesson:
      "Different resource roles and capacity constraints create planning; a row of currencies alone does not.",
    art: "Original crystalline apparatus, concentric etched rings and a three-node resource diagram.",
    motion:
      "Cast → travelling light → receiving resource → newly active connection.",
    test: "Can a player identify which conversion and storage upgrade make the final action possible?",
  },
  {
    id: "hamlet",
    title: "Pocket Hamlet",
    genre: "Creative sandbox",
    source: "Gourdlets",
    url: "https://aunty-games.itch.io/gourdlets",
    accent: "#679073",
    background: "#dce7ce",
    premise:
      "Place a little settlement and watch original residents stroll among the buildings.",
    lesson:
      "Creation, composition and observing an inhabited world can be enough; points are optional.",
    art: "An original isometric island with cedar trees, cottages, a workshop, paths and tiny residents.",
    motion:
      "Placement ghost → settling building → wandering residents and chimney smoke.",
    test: "Does a new arrangement produce a world the player wants to keep looking at?",
  },
];
export const byId = Object.fromEntries(
  patterns.map((pattern) => [pattern.id, pattern]),
);
