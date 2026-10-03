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
  const heading=document.createElement('strong');heading.textContent=title; const arrow=document.createElementNS('http://www.w3.org/2000/svg','svg'); arrow.setAttribute('class','icon'); arrow.setAttribute('aria-hidden','true'); const use=document.createElementNS('http://www.w3.org/2000/svg','use'); use.setAttribute('href','#i-arrow-up-right'); arrow.append(use);heading.append(arrow);
  const caption=document.createElement('span');caption.textContent=detail;
  card.append(img,heading,caption);card.addEventListener('click',()=>showImage(img.src,title+' / '+detail));grid.append(card);
 });
 document.querySelectorAll('[data-gallery]').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.gallery===platform)));
}
document.querySelectorAll('[data-gallery]').forEach(button=>button.addEventListener('click',()=>renderGallery(button.dataset.gallery)));
renderGallery('windows');


const appearanceImage=document.querySelector('#appearance-shot');
const appearanceFrame=document.createElement('div');appearanceFrame.className='appearance-image-frame';appearanceImage.before(appearanceFrame);appearanceFrame.append(appearanceImage);
const appearanceOverlay=appearanceImage.cloneNode();appearanceOverlay.removeAttribute('id');appearanceOverlay.setAttribute('aria-hidden','true');appearanceOverlay.alt='';appearanceOverlay.loading='eager';appearanceOverlay.className='appearance-image-overlay';appearanceFrame.append(appearanceOverlay);

let appearanceRequest=0, selectedAppearance='light', selectedSoftwareColor='teal';
const softwareColors={teal:{name:'青绿',light:'windows-dashboard.png',dark:'windows-dashboard-dark.png'},blue:{name:'蓝色',light:'windows-blue-light.png',dark:'windows-blue-dark.png'},orange:{name:'橙色',light:'windows-orange-light.png',dark:'windows-orange-dark.png'}};
async function renderAppearance(){
 const request=++appearanceRequest;const mode=selectedAppearance;const color=softwareColors[selectedSoftwareColor];
 const src='assets/'+color[mode];const preload=new Image();preload.src=src;
 try{await preload.decode();}catch{if(request===appearanceRequest)document.querySelector('#color-status').textContent='画面加载失败，请重新选择';return;}
 if(request!==appearanceRequest)return;
 appearanceOverlay.src=appearanceImage.src;appearanceOverlay.style.opacity='1';appearanceImage.src=src;
 appearanceImage.alt='Windows '+color.name+'主题 · '+(mode==='dark'?'深色':'浅色')+'实际运行截图';
 requestAnimationFrame(()=>requestAnimationFrame(()=>{if(request===appearanceRequest)appearanceOverlay.style.opacity='0';}));
 document.querySelector('.appearance-preview').classList.toggle('is-dark',mode==='dark');
 document.querySelector('#appearance-caption').textContent='Windows · '+color.name+'主题 · '+(mode==='dark'?'深色模式':'浅色模式')+' · 实际运行截图';
 document.querySelector('#color-status').textContent='正在应用：'+color.name+' · '+(mode==='dark'?'深色':'浅色');
 document.querySelectorAll('[data-appearance]').forEach(item=>item.setAttribute('aria-pressed',String(item.dataset.appearance===mode)));
 document.querySelectorAll('[data-color]').forEach(item=>item.setAttribute('aria-pressed',String(item.dataset.color===selectedSoftwareColor)));
}
document.querySelectorAll('[data-appearance]').forEach(button=>button.addEventListener('click',()=>{selectedAppearance=button.dataset.appearance;renderAppearance();}));
document.querySelectorAll('[data-color]').forEach(button=>button.addEventListener('click',()=>{selectedSoftwareColor=button.dataset.color;renderAppearance();}));
