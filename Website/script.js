const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
if ('IntersectionObserver' in window && !reducedMotion) {
  document.documentElement.classList.add('js-motion');
  const observer = new IntersectionObserver(entries => entries.forEach(entry => {
    if (entry.isIntersecting) { entry.target.classList.add('visible'); observer.unobserve(entry.target); }
  }), { threshold: 0.08 });
  document.querySelectorAll('.reveal').forEach(element => observer.observe(element));
}
const shots = {
 windows: [['windows-dashboard.png','仪表盘','WinUI 3 · Windows 实际运行'],['windows-archive.png','专注档案','日历与日志 · Windows 实际运行']],
 phone: [['phone-dashboard.png','随身专注','Android 手机模拟器 · 实际运行'],['phone-archive.png','看见积累','Android 手机模拟器 · 实际运行']],
 tablet: [['tablet-dashboard.png','大屏工作台','Android 平板模拟器 · 实际运行'],['tablet-courses.png','一周课程','Android 平板模拟器 · 实际运行']]
};
const viewer = document.querySelector('#image-viewer');
let previousFocus;
function showImage(src,title) {
 previousFocus=document.activeElement;
 viewer.querySelector('img').src=src;viewer.querySelector('img').alt=title;
 viewer.querySelector('p').textContent=title;viewer.showModal();
}
viewer.querySelector('button').addEventListener('click',()=>viewer.close());
viewer.addEventListener('click',event=>{if(event.target===viewer)viewer.close();});
viewer.addEventListener('close',()=>previousFocus?.focus());
function renderGallery(platform){
 const grid=document.querySelector('#gallery-grid');grid.className='gallery-grid '+platform;grid.replaceChildren();
 shots[platform].forEach(([file,title,detail])=>{
  const card=document.createElement('button');card.className='gallery-card';
  const img=document.createElement('img');img.src='assets/'+file;img.alt=title;img.loading='lazy';
  const heading=document.createElement('strong');heading.textContent=title+' ↗';
  const caption=document.createElement('span');caption.textContent=detail;
  card.append(img,heading,caption);card.addEventListener('click',()=>showImage(img.src,title+' / '+detail));grid.append(card);
 });
 document.querySelectorAll('[data-gallery]').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.gallery===platform)));
}
document.querySelectorAll('[data-gallery]').forEach(button=>button.addEventListener('click',()=>renderGallery(button.dataset.gallery)));
renderGallery('windows');
