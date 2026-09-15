# Quest Park Tycoon — Product Requirements Document
**Version:** 0.1 (draft) | **Date:** 2026-09-15 | **Author:** Petra (for Gary Keeler)
**Repo:** `iamkeeler/quest-park-tycoon` (public, empty except README)

---

## 1. Vision & Pillars

**One-liner:** The deep management sim of RollerCoaster Tycoon 1 (1999), rebuilt as a first-class VR experience — you don't watch your park from above, you *walk* it.

**Pillars:**

1. **Diegetic god mode.** You're not a cursor hovering over a diorama; you're a giant standing in your park. Reach down, grab a guest, plop a ride with your hands, ride the coaster you just built in first person. Every management action should feel like a physical act.
2. **Sim depth first, theme park second.** The soul of RCT1 is the simulation: E/I/N ratings, guest needs, staff patrol zones, ride reliability decay, pricing economics. We port the *systems*, not the 2D sprites. Depth is the differentiator — other VR park builders are sandbox toys; this one is a tycoon game.
3. **Comfort is a feature.** Riding a coaster you designed is the payoff moment, but VR nausea can kill it. First-class comfort options (coaster cam modes, vignette, horizon-lock) are P0, not settings-page afterthoughts.
4. **Performant on standalone.** Quest 3 is the target, standalone only. No PCVR requirement. The sim must scale down gracefully (guest count, scenery LOD, shadows) so frame budget is never hostage to park size.

**What it's not:** a port, a remake asset flip, or a passive theme-park viewer. If a feature doesn't involve touching, walking through, or riding the park, it needs a strong reason to exist.

---

## 2. Platform & Tech

### Target
- **Hardware:** Meta Quest 3 (standalone). Quest 3S compatibility target, Quest Pro as bonus.
- **Chipset:** Snapdragon XR2 Gen 2. **Render target:** 72 Hz stable; 90/120 Hz experimental for smaller parks.
- **Input:** Controllers (primary) + hand tracking (build mode, UI). Mixed input must not require re-teaching.

### Engine: Unity 2022 LTS+ with Meta XR SDK (RECOMMENDED)
- **Why Unity:** Meta XR SDK (Interaction SDK, hand tracking, haptics, MRUK) is best-supported on Unity; URP is mature for mobile-VR perf tuning; asset pipeline for low-poly stylized art is fast; Gary's pipeline already favors quick iteration.
- **Key packages:** Meta XR Interaction SDK (grab/ray/direct interactions), XR Hands, Meta Avatar SDK (guest/staff low-poly figures are custom; avatars optional for co-op later), MRUK (optional Phase 2: build coaster layouts on your real floor), Unity Input System + XR Interaction Toolkit as fallback layer.
- **Considered alternatives:**
  - *Unreal Engine 5:* superior rendering, but Lumen/Nanite are not Quest-friendly; Meta's UE fork lags Unity's SDK parity; longer iteration loops.
  - *Godot 4:* open and light, but Meta platform support (hand tracking quality, SDK updates) is weaker; smaller hiring/community surface for VR tycoon specifics.
- **Networking:** none in v1 (single-player). Architecture should keep sim state serializable for future multiplayer/co-op spectator mode.

### Performance budget (Quest 3, standalone)
- **Guests:** v1 cap ~300 active peeps (RCT1 did thousands as sprites; 3D low-poly rigged figures are the cost driver). Guest LOD: full animation within ~30 m, billboard/impostor beyond, despawn beyond view + parked in pool.
- **Rides:** one draw call per major ride part via GPU instancing; coaster track instanced per-piece. Target < 250 draw calls for a full park.
- **Terrain:** single chunked mesh (≤ 64×64 tiles), vertex-colored; scenery instanced (trees, lamps, bins as instanced meshes).
- **Frame budget:** ≤ 13.9 ms at 72 Hz. Sim tick at 10 Hz decoupled from render; economy/AI on background job, never main thread.
- **Memory:** stay under ~3.5 GB; texture budget favors stylized flat-shaded art (small atlases) over PBR.

### Tooling notes
- 3D: Blender for ride vehicles, stalls, scenery; procedural track pieces generated in-engine (parametric curves beat hand-modeled track).
- Version control: GitHub, `main` protected, feature branches + PRs. CI: build APK on label `build-apk` (same pattern as other Gary projects).

---

## 3. MVP Scope — Phase 1 ("First Coaster")

The smallest shippable fun slice. If it isn't on this list, it's Phase 2.

**Rides (buildable):**
- 1 coaster type: **steel sit-down looper** (loops + corkscrews supported; the RCT1 "Steel Corkscrew" analog). Full piece-by-piece builder.
- 3 flat rides: **Ferris Wheel** (gentle), **Merry-Go-Round** (gentle), **Swinging Ship** (thrill). Pre-built footprints, placed as whole units — no piece building.

**Stalls:** 4 — Burger Stall, Drinks (lemonade), Cotton Candy, Information Kiosk (maps + umbrellas).

**Guest sim v1:** spawn at entrance, wallet, needs (hunger, thirst, nausea), intensity preference, ride choice + queuing, thoughts feed, leave-when-unhappy/broke. (Bladder/tiredness/litter deferred to Phase 2.)

**Staff v1:** Handyman (sweep/vomit cleanup) + Mechanic (inspect + repair). Patrol zones via hand painting. No security/entertainer in v1.

**Economy v1:** admission fee + per-ride pricing (RCT1 formula), $10k starting loan + interest, ride build/operating costs, monthly staff wages. No research, no marketing, no awards in v1.

**Park rating:** 0–999 computed from ride variety/quality, cleanliness, guest happiness. Displayed on wrist UI.

**Scenarios:** 1 guided scenario (guest quota + rating target) + 1 sandbox mode.

**VR interactions (v1):** teleport + smooth locomotion, grab guests, hand-driven coaster piece placement, physical price sliders, first-person coaster test ride with comfort options.

**Exit criteria (Phase 1 done):** build a coaster → test-ride it → open park → 200 guests arrive → park rating ≥ 600 sustained → 72 Hz held with 200 guests on device.

---

## 4. Full Feature Spec (adapted from RCT1)

### 4.1 Rides catalog (post-MVP target)
- **Transport:** Miniature Railroad, Monorail, Chairlift — guests use them as actual transit; line must connect two stations.
- **Gentle:** Ferris Wheel, Merry-Go-Round, Observation Tower, Car Ride, Spiral Slide, Maze, Miniature Golf, Haunted House, Circus, Ghost Train, Crooked House.
- **Coasters:** start with steel looper (v1); add Wooden, Steel Twister, Inverted, Suspended Swinging, Bobsled, Mine Train, Virginia Reel in Phase 2+. Free-build for all coaster types; ~14 types is the RCT1-complete goal.
- **Thrill:** Twist, Launched Freefall, Swinging Ship, Go Karts, Motion Simulator, 3D Cinema, Top Spin.
- **Water:** Log Flume, Dinghy Slide, River Rapids, Boat Hire — water rides surge on hot days.
- **Shops & stalls:** full RCT1 food/drink/souvenir set (burger, fries, pizza, ice cream, cotton candy, popcorn, donut; lemonade, coffee; balloon, t-shirt, hat; info kiosk with umbrellas; bathrooms as service buildings).

### 4.2 Coaster builder — VR interaction design
- **Piece palette:** a floating "toolbox" on the non-dominant hand lists pieces (straight, curve L/R, slope up/down, banked curve, helix, S-bend, loop, corkscrew, station, lift hill). Point with dominant hand and pull trigger to select.
- **Placement:** aim a ghost preview at the terrain grid; the ghost snaps to the last placed piece's endpoint (chain-building is the default). Squeeze trigger to commit. Bank/height adjusted with thumbstick while ghost is active. Undo = flick wrist gesture or button.
- **Lift hills:** chain lift or launched lift piece; no lift = coaster won't complete circuit (validated before test).
- **Test mode:** press the big physical "TEST" lever on the station → camera cuts to first-person front-seat ride. You *ride* the layout. Physics sim computes stats live: max/avg speed, ride time, Gs, drops, air time → **Excitement / Intensity / Nausea** ratings on exit.
- **Tuning loop:** E/I/N panel floats beside the track; grab and drag individual pieces to reshape, re-test in seconds. Scenery near track boosts Excitement; Intensity > ~10 tanks it (guests refuse). Keep ride time under ~4 min or guests complain.
- **Modes:** continuous circuit, shuttle, powered launch; trains/cars count, full/partial load, wait time, colors, music — all as physical station-side controls.

### 4.3 Guest AI
- Named "Guest #N", randomized cash, needs meters: Hunger, Thirst, Tiredness, Bladder, Nausea + preferred intensity + nausea tolerance (Phase 2 adds bladder/tiredness/litter).
- Behaviors: seek stalls when hungry/thirsty; bathrooms when bladder high (else vomit — handymen clean it); benches when tired/nauseous; bins for trash (else litter → vandalism clusters); buy umbrellas when raining (at ANY price — RCT1 rule preserved).
- Ride choice matches intensity preference and wallet; queues > ~9 min cause complaints and line-leaving (entertainers at queue tails mitigate, Phase 2).
- Leave when unhappy, broke, or after enough time. Carry inventory: food/drink/souvenirs/umbrellas/on-ride photos.
- **Recent-thoughts** per guest ("I'm not paying that much for X", "The paths here are disgusting") feeds Park Rating.
- **Vandals** (Phase 2): red-faced guests smash benches/lamps/bins; cluster where litter/vomit is high; countered by handymen, happy guests, security guards (Phase 2 staff).

### 4.4 Staff
- 4 types, monthly wages, grabbable/relocatable by the hand, renamable, paintable patrol zones:
  - **Handyman** ($35–50/mo): sweep, empty bins, water gardens, mow grass — toggled task list. *v1*
  - **Mechanic** ($55–80/mo): inspect rides on 10-min intervals (reliability % decays; prevents breakdowns); radio-dispatched to breakdowns, enters via ride exit (exit must be path-reachable). *v1*
  - **Security Guard** ($45–60/mo): passive vandalism deterrent. *Phase 2*
  - **Entertainer** ($40–55/mo): costumes (panda/tiger/elephant + unlockables), boosts nearby happiness, best at long queue tails. *Phase 2*
- Staff have thought bubbles; unhappy/overworked staff quit (Phase 2).

### 4.5 Economy & pricing (RCT1 formulas preserved)
- **Admission:** $40–60 typical; free admission allowed (guests spend more in-park).
- **Per-ride pricing:** ≈ Excitement rating rounded down for thrill/coasters, adjusted ±(Intensity−Excitement)/10; gentle rides = excitement rounded down. Ride value **depreciates with age** — lower prices or rebuild to refresh. Duplicate ride type: −25% value each.
- **Entrance fee rule:** Park Value ÷ 2000 ≈ max fee without complaints.
- **Loans:** $10k typical start, scenario-capped max, interest accrues monthly. Land purchasable per tile.
- **Research & Development** (Phase 2): fundable None→Maximum; categories researched independently (Transport, Gentle, Coasters, Thrill, Water, Shops/Stalls, Scenery, Ride Improvements). Scenarios gate the queue.
- **Marketing** (Phase 2): 1–6 week campaigns — park advertising (~$5k/6 wks → ~+600 guests), free admission vouchers, reduced-fee vouchers, free ride vouchers; arrival delay modeled.
- **Weather:** sunny/cloudy/rain; per-scenario temperature; rain tanks happiness unless umbrellas (kiosk-only, price at max).
- **Awards** (Phase 2, monthly, cash bonus/penalty): Best Value ±$800, Most Thrilling $700, Best Coasters $500, Best/Worst Food ±$300, Best Toilets $300, Best Staff $300, themed scenery $200.

### 4.6 Landscape & park building
- Raise/lower terrain per tile (cost per tile), dig water, buy/remove trees. Paths + **queue lines as a separate path type** (required at ride entrances). Path extras: benches, bins, lamps. Scenery categories (trees, gardens, walls, statues, themed sets) boost nearby ride excitement + park rating. Rides over water get excitement bonus. Underground building supported (saves costs, adds thrill).

### 4.7 Scenarios & objectives
- RCT1 structure: scenario list with objectives — guest quota + Park Rating ≥ 600 (e.g., 1,000 guests by October Year 4), or Park Value targets (e.g., $75k). Calendar: 8 months/year (March–October), ~7 days/month.
- Completing all scenarios unlocks **Mega Park sandbox**. Phase 1 ships 1 scenario + sandbox.

---

## 5. VR-Specific Design

### Locomotion
- **Teleport** (arc pointer) and **smooth locomotion** (thumbstick), both available; snap turn default, smooth turn optional. Seated/standing supported.
- **God-scale toggle:** shrink yourself to walk the paths at guest scale, or grow to park-overlook scale for building. Pinch/scale gesture or wrist menu.

### Comfort (coaster riding)
- Three ride-cam modes: **Full** (locked to train, full motion), **Comfort** (dynamic vignette + horizon-stabilized, reduced banking visual), **Cinematic** (chase cam — watch your train from outside, zero vection). Default: Comfort.
- Global comfort: vignette strength slider, snap-turn angle, optional "tunnel vision" during high-G segments (auto).

### Interactions
- **Grab peeps:** pick up any guest or staff member, read their thought bubble up close, drop them where you want (dropping a guest in water = classic; guests react).
- **Paint patrol zones:** hold trigger and sweep hand over terrain to paint staff zones, path networks, or scenery brush areas.
- **Physical UI:** price sliders are grabbable levers; ride mode switches are chunky toggles; the TEST lever is a real lever. Numbers shown on flip-disc style displays.
- **Diegetic vs wrist:** in-world physical controls for rides/stalls; a **wrist-mounted park dashboard** (left wrist) for global info — cash, loan, park rating, date, guest count, alerts. Full management screens (research, marketing, staff list) live as a "park office" — a physical shack you walk into, with wall-mounted boards.

### Social/presence (Phase 3)
- Co-op spectator: friend joins as a guest-scale visitor walking your park while you build. No shared editing in v1.

---

## 6. Non-Goals for v1
- Multiplayer / shared editing / networked sim.
- MRUK mixed-reality mode (build on your real floor) — Phase 2 experiment.
- Full RCT1 ride catalog (see §4.1 phasing); water rides, transport rides, entertainers, security, vandals all Phase 2+.
- Research, marketing, awards systems — Phase 2.
- Quest 3S-specific optimization pass; PCVR port.
- User-generated content sharing / workshop; licensed IP themes.
- Voice control.

---

## 7. Milestones & Phases

| Phase | Name | Scope | Exit criteria |
|---|---|---|---|
| 0 | Spike | Unity + Meta XR SDK project boots on Quest 3 at 72 Hz; one grabbable cube; terrain tile mesh renders. | Runs on device, no red. |
| 1 | First Coaster (MVP) | §3 scope. | Build → test-ride → open park → 200 guests → rating ≥ 600 sustained → 72 Hz held. |
| 2 | Full Tycoon | Water + transport rides, 6+ coaster types, vandals, security/entertainer, research, marketing, awards, weather economy, 6 scenarios. | Complete one full scenario start-to-finish on device; rating/award systems verified. |
| 3 | Polish & Presence | MRUK experiment, co-op spectator, Mega Park sandbox, comfort tuning from playtests, App Lab submission build. | 10 external playtesters, <5% motion-sickness dropout, submission-ready. |

Each phase ends with a tagged release + APK artifact (`build-apk` label pattern). No phase merges without an on-device smoke test — nothing ships on emulator claims alone.

---

## 8. Open Questions for Gary
1. **Art direction:** stylized low-poly flat-shaded (cheap, charming, fast) vs. a more detailed "miniature model" look (costlier)? This drives the whole asset budget.
2. **Scenario 1 setting:** classic greenfield park, or a themed starter (e.g., beachfront with water-ride hook — but water rides are Phase 2, so greenfield keeps MVP honest)?
3. **Pricing model for the game itself:** free App Lab experiment, paid release, or portfolio piece only? Affects how much polish Phase 3 gets.
4. **Hand tracking priority:** nice-to-have alongside controllers, or a headline feature (build mode fully hand-driven)? Full hand-driven building is significantly more work.
5. **Co-op spectator in Phase 3 — keep, cut, or promote?** It's the most technically risky item on the roadmap; say now if it's not worth it.
