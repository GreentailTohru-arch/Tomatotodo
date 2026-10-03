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
  const kind=platform==='windows'?'laptop':platform==='phone'?'phone':'tablet';
  const device=document.createElement('div');device.className='device-mockup mockup-'+kind;
  const screen=document.createElement('div');screen.className='device-screen';screen.append(img);device.append(screen);
  if(kind==='laptop'){const deck=document.createElement('div');deck.className='laptop-deck';deck.setAttribute('aria-hidden','true');deck.innerHTML='<span class="keyboard"></span><span class="trackpad"></span>';device.append(deck);}
  card.append(device,heading,caption);card.addEventListener('click',()=>showImage(img.src,title+' / '+detail));grid.append(card);if(kind==='laptop')applyPhotographicLaptop(device);
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
const desktopGuide = {
 dashboard:{kicker:'开始专注',title:'仪表盘',description:'把计时、任务和日常信息放在同一张工作台上。',steps:[['选中当前任务','在清单组件中选择本次要做的任务，让专注记录有明确的归属。'],['开始、暂停与重置','使用右下角的计时按钮控制本次专注；计划时段显示专注和短休的时长。'],['整理你的工作台','点击右上角编辑按钮，添加或移除组件、调整排列，完成后保存布局。']],tip:'想保持计时可见？可启用小窗模式；需要减少干扰时，可启用沉浸模式。',image:'windows-dashboard.png'},
 presets:{image:'windows-presets.png',kicker:'安排下一步',title:'配置',description:'用多套清单组织任务，让学习、工作与日常安排各有位置。',steps:[['创建或编辑清单','添加清单，在清单的更多菜单中编辑名称与任务内容。'],['补充任务信息','输入任务标题；按需添加副标题和预计番茄数量。'],['调整顺序与完成状态','通过排序入口整理清单和任务顺序；完成任务后勾选对应事项。']],tip:'先准备好清单，再到仪表盘选择当前任务并开始计时。清单名称和任务内容由你自行填写。'},
 archive:{kicker:'回顾真实投入',title:'档案',description:'通过日历、日志、统计与热力图，回顾已经记录的专注。',steps:[['查看一天的记录','在专注日历中选择日期，旁边的日志会显示该日的任务与专注时长。'],['切换统计范围','使用日度、周度、月度或年度统计，比较不同时间范围的投入。'],['观察长期节奏','查看热力图；悬停在统计点或热力格上，可以查看具体专注时长。']],tip:'档案展示实际记录。需要分享时，可使用页面右上角的导出入口。',image:'windows-archive.png'},
 courses:{image:'windows-courses.png',kicker:'掌握课程安排',title:'课程表',description:'集中查看一周课程，并与仪表盘课程组件和提醒配合使用。',steps:[['导入课程数据','前往「常规 → 课程表」，使用课程表导入入口载入课程安排。'],['查看课程详情','在课程页面查看课程名称、时间、教室与教师，确认当天和本周安排。'],['设置课程提醒','在常规的课程提醒设置中启用需要的提醒方式与提前时间。']],tip:'找不到课程表入口时，先检查常规中的课程表导航开关。请使用软件提供的课程数据模板。'},
 tools:{image:'windows-tools.png',kicker:'随手处理小事',title:'工具',description:'从软件中快速打开常用系统工具和翻译服务。',steps:[['记下临时想法','打开 Windows 系统便笺，创建或管理临时笔记。'],['进行计算与计时','使用计算器入口打开系统计算器；秒表入口打开 Windows 时钟。'],['查询翻译','点击翻译入口，在默认浏览器中打开 Bing 翻译。']],tip:'这些入口调用 Windows 系统应用或外部网站；工具是否可用取决于本机应用与网络状态。'},
 general:{image:'windows-general.png',kicker:'按你的习惯设置',title:'常规',description:'集中管理计时、外观、课程、账户与版本信息。',steps:[['设置计时节奏','在通用设置中调整专注与短休时长、启用短休，选择手动或自动循环；正向计时用于累计时间。'],['调整软件外观','进入「个性化」，选择浅色、深色或自动模式，并调整主题色与显示大小。'],['管理账户与数据','在账户与用户数据中处理登录、同步、迁移和数据导入导出；在关于与更新中检查更新、查看公告与软件介绍。']],tip:'调整计时时长后，按软件提示重置计时器使新时长生效。迁移或覆盖数据前，先导出备份。'}
};
function renderDesktopGuide(key){const item=desktopGuide[key];if(!item)return;document.querySelectorAll('[data-guide]').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.guide===key)));document.querySelector('#guide-kicker').textContent=item.kicker;document.querySelector('#guide-title').textContent=item.title;document.querySelector('#guide-description').textContent=item.description;const list=document.querySelector('#guide-steps');list.replaceChildren();item.steps.forEach(([title,detail])=>{const row=document.createElement('li');const heading=document.createElement('strong');heading.textContent=title;row.append(heading,document.createTextNode(detail));list.append(row);});document.querySelector('#guide-tip').textContent=item.tip;const figure=document.querySelector('#guide-figure');figure.hidden=!item.image;if(item.image){const img=document.querySelector('#guide-image');img.src='assets/'+item.image;img.alt='Windows '+item.title+'实际运行截图';}}
document.querySelectorAll('[data-guide]').forEach(button=>button.addEventListener('click',()=>renderDesktopGuide(button.dataset.guide)));renderDesktopGuide('dashboard');

 document.querySelector('#guide-image-button').addEventListener('click',()=>{const img=document.querySelector('#guide-image');showImage(img.src,img.alt);});
function applyPhotographicLaptop(device){
 if(device.dataset.photographic)return;
 device.dataset.photographic='true';device.classList.add('photographic-laptop');
 const screen=device.querySelector('.device-screen');
 const observer=new ResizeObserver(()=>{
  const k=device.clientWidth/1536;
  const src=[[0,0],[1000,0],[1000,600],[0,600]];
  const dest=[[400,166],[1425,115],[1417,743],[386,721]].map(([x,y])=>[x*k,y*k]);
  const rows=[];
  src.forEach(([x,y],i)=>{const [u,v]=dest[i];rows.push([x,y,1,0,0,0,-u*x,-u*y,u],[0,0,0,x,y,1,-v*x,-v*y,v]);});
  for(let c=0;c<8;c++){
   let pivot=c;for(let r=c+1;r<8;r++)if(Math.abs(rows[r][c])>Math.abs(rows[pivot][c]))pivot=r;
   [rows[c],rows[pivot]]=[rows[pivot],rows[c]];
   const factor=rows[c][c];for(let j=c;j<9;j++)rows[c][j]/=factor;
   for(let r=0;r<8;r++)if(r!==c){const n=rows[r][c];for(let j=c;j<9;j++)rows[r][j]-=n*rows[c][j];}
  }
  const [a,b,c,d,e,f,g,h]=rows.map(row=>row[8]);
  screen.style.transform=`matrix3d(${a},${d},0,${g},${b},${e},0,${h},0,0,1,0,${c},${f},0,1)`;
 });observer.observe(device);
}
document.querySelectorAll('.mockup-laptop').forEach(applyPhotographicLaptop);
// One animation frame batches scroll progress; pointer glow is local to download cards.
const readingProgress=document.createElement('div');readingProgress.className='reading-progress';readingProgress.setAttribute('aria-hidden','true');document.body.append(readingProgress);
let progressFrame=0;
function updateReadingProgress(){if(progressFrame)return;progressFrame=requestAnimationFrame(()=>{const length=document.documentElement.scrollHeight-innerHeight;readingProgress.style.transform=`scaleX(${length>0?scrollY/length:0})`;progressFrame=0;});}
if(!reducedMotion){addEventListener('scroll',updateReadingProgress,{passive:true});addEventListener('resize',updateReadingProgress);updateReadingProgress();}
if(matchMedia('(hover:hover) and (pointer:fine)').matches){document.querySelectorAll('.download-card').forEach(card=>{card.addEventListener('pointermove',event=>{const rect=card.getBoundingClientRect();card.style.setProperty('--pointer-x',`${event.clientX-rect.left}px`);card.style.setProperty('--pointer-y',`${event.clientY-rect.top}px`);});});}
