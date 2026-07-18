<div align="center">
  <img src="apps/web/public/piro.svg" alt="Piro" width="72" height="72" />
  <h1>Why Piro exists</h1>
  <p><em>The story and the reasoning behind the project.</em></p>
</div>

---

## The problem we kept running into

Every team that ships software eventually needs three things that are, annoyingly, sold separately:

1. **Uptime monitoring** — is the service actually up, from more than one place?
2. **A status page** — a public, honest signal to users when something is wrong.
3. **Incident response** — getting the right person paged, and coordinating the fix.

The market answer is to buy three products: an uptime monitor, a hosted status page, and an on-call/paging tool. Each is priced per-seat or per-monitor, each holds a slice of your operational data, and none of them talk to each other without glue. For a small engineering team the combined bill is absurd relative to what's actually being done — and every one of them ships your monitoring data, your incident history, and your team's contact details off to someone else's servers.

We didn't want to assemble that stack. We wanted one thing we could run ourselves.

## What we wanted instead

A single, self-hosted platform where:

- **The data is ours.** Checks, incidents, uptime history, and on-call details live in *our* PostgreSQL, on *our* infrastructure. No phone-home, no per-seat telemetry, no vendor holding the export button hostage.
- **Monitoring and response are one system.** An alert, the on-call rotation it pages, the escalation policy behind it, and the incident it may become are the same platform — not four integrations duct-taped together.
- **It runs anywhere.** From a single container on one box, all the way to workers spread across regions and clouds — same binaries, same config.
- **It's honest to users.** A status page should reflect reality, and turning an internal alert into a public incident should be a *deliberate* act, never an automatic one that surprises your customers.

That combination didn't exist as something we could self-host and own. So we built it.

## Built for ourselves, then opened up

We didn't set out to build a monitoring platform — we set out to *use* one. We never even got as far as paying for SaaS: the hosted options were both expensive and, individually, incomplete. PagerDuty starts around **$21/user/month** and only does paging — the actual monitoring lives somewhere else, so you end up stitching two or three products together and paying, realistically, **$50+/seat/month** just to know whether your services are up and page someone when they aren't. For a team, that adds up fast for something so fundamental.

So we skipped SaaS entirely and went open-source and self-hosted, trying the options one at a time: [Kener](https://github.com/rajnandan1/kener) and several others in the self-hosted status-page and uptime space. Each was good at part of the job, but we kept hitting the same wall — none was enterprise-grade, or at least we never found one that was: no real on-call and escalation, thin RBAC and SSO, no multi-region checks, no clean separation between an internal alert and a customer-facing incident. Eventually the honest conclusion was that the tool we wanted didn't exist as open source, so we'd have to build it.

In fact, **this repository originally began as a Kener project** — Kener was the closest starting point we had, and Piro's earliest commits built on it (which is why the first UI was SvelteKit, Kener's stack). From there it diverged completely: the backend was rewritten around ASP.NET Core with a multi-region worker model, the frontend was rebuilt as a Next.js public page plus a Vite admin panel, and the on-call, escalation, incident, and integration layers were added from scratch. What remains today is very much its own platform, but we're glad to name where it started.

Piro started in mid-2026 as an internal tool at [heva](https://heva.co) to monitor our own services. We run it in production and we dogfood every feature — the on-call schedules page our own engineers, the escalation policies are the ones that wake us up, and the status page is the one we'd point our own users at.

Building it for ourselves first is why the feature set leans the way it does: toward the operational reality of a team on call, not toward a demo. Verified notification channels exist because a typo in a phone number meant a missed page. Escalation re-tries and re-escalation exist because the first person paged isn't always the one who answers. Manual alert-to-incident linking exists because we got burned by tools that published customer-facing incidents automatically.

The check engine grew from simple HTTP pings to DNS, SSL, TCP, Ping, gRPC, and cloud-job checks, and the alerting path matured into real on-call rotations and escalation. Once the core was stable enough to trust in production, opening it up was the natural next step.

## Why open source, and why AGPL

Monitoring and status pages are critical infrastructure. We think teams shouldn't have to choose between vendor lock-in and building it all from scratch — there should be a third option you can read, run, and modify.

We chose the **AGPL-3.0** deliberately. It means:

- **You can self-host Piro freely** — for your own internal use, with private modifications, with no obligation to publish anything.
- **If you offer Piro to others as a service**, the AGPL requires you to share your modifications under the same license. This keeps the project a genuine commons rather than free R&D for a closed competitor.

It's the license that best matches our intent: give the software away to the people running it, while keeping the improvements flowing back.

## What Piro is — and isn't

**Piro is** a self-hosted, enterprise-grade status page, uptime monitoring, and incident-response platform. It's for teams that want to own their monitoring stack end to end.

**Piro isn't** a hosted SaaS you sign up for, and it isn't trying to be an all-in-one observability suite. It doesn't replace your metrics/log/trace pipeline (Prometheus, Grafana, OpenTelemetry, and friends) — it sits alongside them, answering "is it up, who's on call, and what do we tell users?" rather than "why is p99 latency high?"

## Where it's going

Larger design decisions go through a public [RFC process](docs/rfcs/) before they're built — the RFC index is the clearest view of what's coming and why. If Piro is useful to you, the best contribution is real-world feedback from running it — that's exactly the loop that shaped it in the first place.

---

<div align="center">
  <sub>Built with ♥ by <a href="https://heva.co">heva</a> · <a href="mailto:devops@heva.co">devops@heva.co</a></sub>
</div>
