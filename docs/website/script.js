// Nav shrink on scroll
const nav = document.getElementById("nav");
const onScroll = () => nav.classList.toggle("scrolled", window.scrollY > 24);
window.addEventListener("scroll", onScroll, { passive: true });
onScroll();

// Reveal on scroll
const io = new IntersectionObserver(
  (entries) => {
    for (const e of entries) {
      if (e.isIntersecting) {
        e.target.classList.add("in");
        io.unobserve(e.target);
      }
    }
  },
  { threshold: 0.12, rootMargin: "0px 0px -40px 0px" }
);
document.querySelectorAll(".reveal").forEach((el) => io.observe(el));

// Card cursor glow
document.querySelectorAll(".card").forEach((card) => {
  card.addEventListener("pointermove", (ev) => {
    const r = card.getBoundingClientRect();
    card.style.setProperty("--mx", `${ev.clientX - r.left}px`);
    card.style.setProperty("--my", `${ev.clientY - r.top}px`);
  });
});

// Mark degraded uptime bars in the status-page mockup
document.querySelectorAll("[data-bars]").forEach((el) => {
  if (el.hasAttribute("data-degraded")) el.classList.add("degraded");
});

// Count-up stats
const countEls = document.querySelectorAll("[data-count]");
const cio = new IntersectionObserver(
  (entries) => {
    for (const e of entries) {
      if (!e.isIntersecting) continue;
      const el = e.target;
      const target = parseInt(el.dataset.count, 10);
      let cur = 0;
      const step = Math.max(1, Math.round(target / 30));
      const tick = () => {
        cur = Math.min(target, cur + step);
        el.textContent = cur;
        if (cur < target) requestAnimationFrame(tick);
      };
      tick();
      cio.unobserve(el);
    }
  },
  { threshold: 0.5 }
);
countEls.forEach((el) => cio.observe(el));

// Live "updated Xs ago" clock in the status card
const clock = document.querySelector("[data-clock]");
if (clock) {
  let s = 0;
  setInterval(() => {
    s = (s + 1) % 60;
    clock.textContent = s === 0 ? "just now" : `${s}s ago`;
  }, 1000);
}
