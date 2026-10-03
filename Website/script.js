const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
if ('IntersectionObserver' in window && !reducedMotion) {
  document.documentElement.classList.add('js-motion');
  const observer = new IntersectionObserver(entries => entries.forEach(entry => {
    if (entry.isIntersecting) { entry.target.classList.add('visible'); observer.unobserve(entry.target); }
  }), { threshold: 0.08 });
  document.querySelectorAll('.reveal').forEach(element => observer.observe(element));
}
const heatmap = document.querySelector('.heatmap');
for (let index = 0; index < 90; index++) {
  const cell = document.createElement('i');
  cell.dataset.level = (index * 7 + Math.floor(index / 6)) % 4;
  cell.setAttribute('aria-hidden', 'true');
  heatmap.appendChild(cell);
}
const accents = { green: '#365d43', coral: '#9d5b40', blue: '#526b99', purple: '#796185' };
document.querySelectorAll('[data-accent]').forEach(button => button.addEventListener('click', () => {
  document.documentElement.style.setProperty('--accent', accents[button.dataset.accent]);
  document.querySelectorAll('[data-accent]').forEach(item => item.setAttribute('aria-pressed', String(item === button)));
}));
