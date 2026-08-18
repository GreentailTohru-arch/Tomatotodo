import { useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import {
  ArrowCounterClockwise,
  ArrowLeft,
  ArrowRight,
  ArrowSquareOut,
  Backspace,
  Calculator,
  CalendarBlank,
  CaretLeft,
  CaretDown,
  CaretRight,
  CaretUp,
  ChartBar,
  Check,
  CircleHalfTilt,
  Clock,
  CloudSun,
  CornersIn,
  CornersOut,
  Database,
  DownloadSimple,
  DotsThreeVertical,
  DotsSixVertical,
  FloppyDisk,
  Flag,
  FolderOpen,
  Gear,
  ListChecks,
  List,
  MapPin,
  Moon,
  MusicNotes,
  NotePencil,
  Palette,
  Pause,
  PencilSimple,
  PictureInPicture,
  Play,
  Plus,
  Repeat,
  SkipBack,
  SkipForward,
  Shuffle,
  SlidersHorizontal,
  SortAscending,
  SpeakerHigh,
  SpeakerSlash,
  Stop,
  Sun,
  Target,
  TextT,
  Timer,
  TrendUp,
  Trash,
  Translate,
  UploadSimple,
  Wrench,
  X,
} from "@phosphor-icons/react";
import {
  Hct,
  SchemeContent,
  SchemeExpressive,
  SchemeFidelity,
  SchemeFruitSalad,
  SchemeMonochrome,
  SchemeNeutral,
  SchemeRainbow,
  SchemeTonalSpot,
  SchemeVibrant,
  argbFromHex,
  hexFromArgb,
} from "@material/material-color-utilities";
import GlassSurface from "@/components/GlassSurface/GlassSurface.jsx";

const MODES = {
  focus: { label: "专注" },
  short: { label: "短休" },
};

const TASKS_VISIBLE = 5;
const TASK_ROW_STEP = 76;
const TIMER_ORBIT_CIRCUMFERENCE = 2 * Math.PI * 48;
const CONTEXT_COMPONENTS = {
  date: { label: "日期与时间", detail: "日期、星期与当前时间" },
  weather: { label: "实时天气", detail: "定位天气或北京天气" },
  countdown: { label: "倒数日", detail: "自定义目标与剩余天数" },
  calendar: { label: "月历", detail: "当前月份、星期与今日位置" },
  media: { label: "当前媒体", detail: "Spotify 或本地音乐播放" },
};
const BEIJING_WEATHER_LOCATION = {
  latitude: 39.9042,
  longitude: 116.4074,
  label: "北京",
  fallback: true,
};

const SPOTIFY_AUTH_STORAGE_KEY = "moke-spotify-auth";
const SPOTIFY_CLIENT_ID_STORAGE_KEY = "moke-spotify-client-id";
const SPOTIFY_VERIFIER_STORAGE_KEY = "moke-spotify-code-verifier";
const SPOTIFY_STATE_STORAGE_KEY = "moke-spotify-auth-state";
const MEDIA_SOURCE_STORAGE_KEY = "tomatotodo-media-source";
const LOCAL_MEDIA_DB_NAME = "tomatotodo-local-media";
const LOCAL_MEDIA_DB_STORE = "handles";
const LOCAL_MEDIA_HANDLE_KEY = "music-directory";
const LOCAL_AUDIO_PATTERN = /\.(mp3|m4a|aac|wav|ogg|oga|flac|opus)$/i;
const COUNTDOWN_STORAGE_KEY = "moke-countdown";
const QUICK_NOTES_STORAGE_KEY = "moke-quick-notes";
const QUICK_NOTE_GUIDE_VERSION_KEY = "tomatotodo-quick-note-guide-v7";
const QUICK_NOTE_GUIDE_OPEN_KEY = "tomatotodo-quick-note-guide-open";
const QUICK_NOTE_GUIDE_ID = "tomatotodo-app-guide";
const QUICK_NOTE_DELETE_HOLD_MS = 2000;
const QUOTE_MODE_STORAGE_KEY = "tomatotodo-quote-mode";
const QUICK_NOTE_LAUNCHER_STORAGE_KEY = "tomatotodo-quick-note-launcher-visible";
const DASHBOARD_GRID_STORAGE_KEY = "tomatotodo-dashboard-grid-v1";
const IMMERSIVE_STORAGE_KEY = "tomatotodo-immersive-mode";
const ANALOG_CLOCK_STORAGE_KEY = "tomatotodo-analog-clock-settings";
const RAIL_COMPACT_STORAGE_KEY = "tomatotodo-rail-compact";
const ACTIVE_TASK_STORAGE_KEY = "tomatotodo-active-task";
const LOCAL_VOLUME_STORAGE_KEY = "tomatotodo-local-volume";
const LOCAL_PLAYBACK_MODE_STORAGE_KEY = "tomatotodo-local-playback-mode";
const USER_DATA_BACKUP_FORMAT = "tomatotodo-user-data";
const USER_DATA_BACKUP_VERSION = 1;
const FACTORY_DEFAULTS_STORAGE_KEY = "tomatotodo-factory-defaults-1.2";
const FACTORY_DEFAULTS_VERSION = 1;
const DASHBOARD_WIDGETS = {
  timer: { label: "大计时器", detail: "横版番茄钟显示", width: 4, height: 2 },
  mode: { label: "专注 / 短休", detail: "切换计时阶段", width: 2, height: 1 },
  countup: { label: "正向计时", detail: "碎片时间记录", width: 1, height: 1 },
  immersive: { label: "沉浸模式", detail: "进入全屏翻页时钟", width: 1, height: 1 },
  miniwindow: { label: "小窗模式", detail: "显示系统置顶计时窗", width: 1, height: 1 },
  tasks: { label: "任务清单", detail: "清单、任务与完成进度", width: 2, height: 2 },
  quote: { label: "名言警句", detail: "专注与成长短句", width: 2, height: 1 },
  active: { label: "正在进行", detail: "当前分配任务", width: 2, height: 1 },
  calendar: { label: "日历", detail: "本月日期", width: 2, height: 2 },
  analogclock: { label: "圆表", detail: "简约时针、分针与秒针", width: 1, height: 2 },
  media: { label: "当前媒体", detail: "Spotify 或本地音乐", width: 2, height: 1 },
  date: { label: "日期与时间", detail: "今日日期和当前时间", width: 2, height: 1 },
  weather: { label: "实时天气", detail: "当前位置天气", width: 2, height: 1 },
  countdown: { label: "倒数日", detail: "距离目标日期", width: 2, height: 1 },
};
const DEFAULT_DASHBOARD_GRID_ORDER = [
  "timer", "tasks", "mode", "countup", "immersive", "miniwindow", "active", "calendar", "analogclock", "date", "weather", "countdown", "media", "quote",
];

function readDashboardGridLayout() {
  try {
    const saved = JSON.parse(localStorage.getItem(DASHBOARD_GRID_STORAGE_KEY));
    const known = new Set(Object.keys(DASHBOARD_WIDGETS));
    const savedOrder = Array.isArray(saved?.order) ? saved.order.filter((id) => known.has(id)) : [];
    const order = [...savedOrder, ...DEFAULT_DASHBOARD_GRID_ORDER.filter((id) => !savedOrder.includes(id))];
    const visible = Object.fromEntries(order.map((id) => [id, saved?.visible?.[id] !== false]));
    return { order, visible };
  } catch {
    return {
      order: [...DEFAULT_DASHBOARD_GRID_ORDER],
      visible: Object.fromEntries(DEFAULT_DASHBOARD_GRID_ORDER.map((id) => [id, true])),
    };
  }
}
const TRANSLATOR_LAUNCHER_STORAGE_KEY = "tomatotodo-translator-launcher-visible";
const CALCULATOR_LAUNCHER_STORAGE_KEY = "tomatotodo-calculator-launcher-visible";
const BING_TRANSLATOR_URL = "https://www.bing.com/translator?to=zh-Hans";
const FOCUS_QUOTES = [
  "日日行，不怕千万里；常常做，不怕千万事。",
  "把今天该做的事做好，明天自然会向你靠近。",
  "专注不是拒绝世界，而是把力量留给最重要的事。",
  "缓慢而坚定，比短暂的热烈更接近成长。",
  "不必一次抵达，持续前进本身就是答案。",
  "每一次沉下心，都是在为未来积蓄力量。",
  "把复杂留给过程，把简单留给当下这一刻。",
  "坚持不是重复昨天，而是让今天比昨天更进一步。",
  "真正的成长，常发生在无人喝彩的专注里。",
  "先完成眼前的一小步，再把远方交给时间。",
  "耐心是一种行动，它让微小的努力慢慢发光。",
  "保持专注，时间会把认真变成看得见的作品。",
];
const DAILY_FORTUNES = [
  { level: "大吉", quip: "今天的专注力正在偷偷超频，别告诉拖延症。" },
  { level: "中吉", quip: "适合先碰最难的任务——放心，只碰五分钟也算开始。" },
  { level: "小吉", quip: "进度会慢慢长出来，前提是别每两分钟挖开看看。" },
  { level: "吉", quip: "今日宜开始，忌打开任务后先整理桌面半小时。" },
  { level: "末吉", quip: "状态可能晚点到账，先开一个番茄钟替它占座。" },
  { level: "平", quip: "运气今天请假了，好在番茄钟还正常上班。" },
  { level: "番茄吉", quip: "今天有番茄加成：认真二十五分钟，焦虑自动少一点。" },
];
const LOGO_PROCRASTINATION_MESSAGES = [
  { title: "不要再摸鱼了", body: "有这功夫，不如先学习五分钟。" },
  { title: "Logo 不会掉落隐藏奖励", body: "但完成一个番茄钟，真的会留下进度。" },
  { title: "检测到高频点击", body: "手速很好，现在把它用在推进任务上吧。" },
  { title: "今日运势已经看过了", body: "真正能改运的，可能是现在开始。" },
  { title: "番茄正在看着你", body: "再点也不会成熟，专注二十五分钟试试。" },
  { title: "短暂休息可以", body: "但别把休息发展成 Logo 压力测试。" },
  { title: "摸鱼额度接近上限", body: "建议立刻打开任务，保住今天的专注记录。" },
  { title: "这不是抽卡按钮", body: "最佳奖励藏在完成任务之后。" },
  { title: "运气加载完毕", body: "接下来该加载你的行动力了。" },
  { title: "点击次数很努力", body: "如果学习也保持这个频率，今天会很不错。" },
];
const QUICK_NOTE_GUIDE_CONTENT = `Tomatotodo 使用说明｜1.2.0

Tomatotodo 是一款结合任务管理、番茄计时与专注记录的桌面应用。

一、快速开始
在“仪表盘”选择任务与专注时段，点击右下角播放按钮开始。计时可暂停或重置；启用短休后，专注与短休自动轮换。完成一轮专注会获得一枚番茄并写入档案。

正向计时适合碎片时间；沉浸模式提供全屏翻页时钟；小窗模式可在其他窗口上方显示当前状态。

二、任务与仪表盘
在“配置”中创建和管理任务清单。任务支持小标题、预计番茄数、完成状态与拖拽排序。

仪表盘进入编辑状态后，可添加、关闭和移动组件；保存后会记住当前布局。

三、专注档案
“档案”展示今日数据、专注日历、单日日志、统计折线图与年度热力图。右上角导出按钮可生成包含今日数据、日志、Logo 与水印的专注明信片。

四、工具与媒体
“工具”包含快捷便笺、翻译、计算器和秒表。便笺自动保存；在便笺列表中长按删除操作 2 秒即可删除。

媒体组件支持 Spotify 与本地音乐。本地文件仅在当前设备读取，不会上传。

五、个性化与数据
“个性化”可调整自动、日间或夜间模式、Material You 主题色、纯黑模式和字体缩放。“常规”提供计时、圆表、倒数日、媒体及数据设置。

用户数据默认保存在本机。“常规”支持导入或导出 JSON 备份；Spotify 登录令牌、本地音乐文件与文件夹授权不会写入备份。恢复出厂数据需要两次确认。

六、快捷键
F12：切换全屏。
F5：刷新天气。
Esc：关闭当前工具窗口。

更新日志｜1.2.0
• 全面升级为 Material Design 3 界面与左侧导航。
• 新增可编辑、可拖拽并自动对齐的组件化仪表盘。
• 重构任务清单配置，支持小标题、预计番茄数和预设排序。
• 重构专注档案，新增统计图、热力图与专注明信片导出。
• 新增沉浸翻页时钟、圆表、秒表及本地音乐控制。
• 完善 Material You 配色、日夜模式、数据备份与响应式布局。`;

function createQuickNoteGuide() {
  const now = new Date().toISOString();
  return {
    id: QUICK_NOTE_GUIDE_ID,
    content: QUICK_NOTE_GUIDE_CONTENT,
    createdAt: now,
    updatedAt: now,
    position: null,
  };
}

function readQuickNotes() {
  let notes = [];
  try {
    const saved = JSON.parse(localStorage.getItem(QUICK_NOTES_STORAGE_KEY));
    if (Array.isArray(saved)) {
      notes = saved
        .filter((note) => note && typeof note.id === "string")
        .map((note) => ({
        id: note.id,
        content: typeof note.content === "string" ? note.content : "",
        createdAt: note.createdAt || new Date().toISOString(),
        updatedAt: note.updatedAt || note.createdAt || new Date().toISOString(),
        position: note.position && Number.isFinite(note.position.x) && Number.isFinite(note.position.y)
          ? note.position
          : null,
        }));
    }
  } catch {
    notes = [];
  }

  try {
    if (localStorage.getItem(QUICK_NOTE_GUIDE_VERSION_KEY) !== "7") {
      const guide = createQuickNoteGuide();
      notes = [guide, ...notes.filter((note) => note.id !== QUICK_NOTE_GUIDE_ID)];
      localStorage.setItem(QUICK_NOTE_GUIDE_VERSION_KEY, "7");
      localStorage.setItem(QUICK_NOTES_STORAGE_KEY, JSON.stringify(notes));
      sessionStorage.setItem(QUICK_NOTE_GUIDE_OPEN_KEY, "1");
    }
  } catch {
    // Storage restrictions must not prevent the app from opening.
  }
  return notes;
}

function readInitialQuickNoteId() {
  try {
    const shouldOpenGuide = sessionStorage.getItem(QUICK_NOTE_GUIDE_OPEN_KEY) === "1";
    sessionStorage.removeItem(QUICK_NOTE_GUIDE_OPEN_KEY);
    return shouldOpenGuide ? QUICK_NOTE_GUIDE_ID : null;
  } catch {
    return null;
  }
}

function quickNoteDate(value, includeTime = true) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "--";
  return date.toLocaleString("zh-CN", includeTime
    ? { month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hour12: false }
    : { year: "numeric", month: "2-digit", day: "2-digit" });
}

function defaultQuickNotePosition() {
  if (typeof window === "undefined") return { x: 420, y: 110 };
  const cardWidth = Math.min(360, window.innerWidth - 32);
  return {
    x: Math.max(16, Math.min(window.innerWidth - cardWidth - 16, window.innerWidth * 0.38)),
    y: Math.max(78, Math.min(window.innerHeight - 430, window.innerHeight * 0.14)),
  };
}

function clampQuickNotePosition(position) {
  if (typeof window === "undefined") return position;
  const cardWidth = Math.min(360, window.innerWidth - 32);
  const cardHeight = Math.min(420, window.innerHeight - 32);
  return {
    x: Math.max(16, Math.min(window.innerWidth - cardWidth - 16, position.x)),
    y: Math.max(16, Math.min(window.innerHeight - cardHeight - 16, position.y)),
  };
}

function defaultTranslatorPosition() {
  if (typeof window === "undefined") return { x: 430, y: 88 };
  const cardWidth = Math.min(560, window.innerWidth - 32);
  return {
    x: Math.max(16, Math.min(window.innerWidth - cardWidth - 16, window.innerWidth * 0.34)),
    y: Math.max(72, Math.min(window.innerHeight - 520, window.innerHeight * 0.1)),
  };
}

function clampTranslatorPosition(position) {
  if (typeof window === "undefined") return position;
  const cardWidth = Math.min(560, window.innerWidth - 32);
  const cardHeight = Math.min(620, window.innerHeight - 32);
  return {
    x: Math.max(16, Math.min(window.innerWidth - cardWidth - 16, position.x)),
    y: Math.max(16, Math.min(window.innerHeight - cardHeight - 16, position.y)),
  };
}

function defaultCalculatorPosition() {
  if (typeof window === "undefined") return { x: 470, y: 110 };
  const cardWidth = Math.min(320, window.innerWidth - 32);
  return {
    x: Math.max(16, Math.min(window.innerWidth - cardWidth - 16, window.innerWidth * 0.42)),
    y: Math.max(72, Math.min(window.innerHeight - 500, window.innerHeight * 0.13)),
  };
}

function clampCalculatorPosition(position) {
  if (typeof window === "undefined") return position;
  const cardWidth = Math.min(320, window.innerWidth - 32);
  const cardHeight = Math.min(500, window.innerHeight - 32);
  return {
    x: Math.max(16, Math.min(window.innerWidth - cardWidth - 16, position.x)),
    y: Math.max(16, Math.min(window.innerHeight - cardHeight - 16, position.y)),
  };
}

function defaultStopwatchPosition() {
  if (typeof window === "undefined") return { x: 500, y: 96 };
  const cardWidth = Math.min(360, window.innerWidth - 32);
  return {
    x: Math.max(16, Math.min(window.innerWidth - cardWidth - 16, window.innerWidth * 0.46)),
    y: Math.max(72, Math.min(window.innerHeight - 520, window.innerHeight * 0.11)),
  };
}

function clampStopwatchPosition(position) {
  if (typeof window === "undefined") return position;
  const cardWidth = Math.min(360, window.innerWidth - 32);
  const cardHeight = Math.min(520, window.innerHeight - 32);
  return {
    x: Math.max(16, Math.min(window.innerWidth - cardWidth - 16, position.x)),
    y: Math.max(16, Math.min(window.innerHeight - cardHeight - 16, position.y)),
  };
}

function formatStopwatchTime(milliseconds) {
  const totalCentiseconds = Math.max(0, Math.floor(milliseconds / 10));
  const minutes = Math.floor(totalCentiseconds / 6000);
  const seconds = Math.floor((totalCentiseconds % 6000) / 100);
  const centiseconds = totalCentiseconds % 100;
  return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}:${String(centiseconds).padStart(2, "0")}`;
}

function readSpotifyAuth() {
  try {
    const saved = JSON.parse(localStorage.getItem(SPOTIFY_AUTH_STORAGE_KEY));
    return saved?.accessToken && saved?.refreshToken ? saved : null;
  } catch {
    return null;
  }
}

function spotifyRedirectUri() {
  if (typeof window === "undefined") return "";
  const redirect = new URL(window.location.href);
  redirect.search = "";
  redirect.hash = "";
  return redirect.toString();
}

function spotifyLoopbackUrl(clientId = "") {
  if (typeof window === "undefined") return "";
  const url = new URL(window.location.href);
  url.hostname = "127.0.0.1";
  url.search = "";
  url.hash = clientId ? new URLSearchParams({ spotify_client_id: clientId }).toString() : "";
  return url.toString();
}

function randomSpotifyString(length = 64) {
  const alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
  const values = crypto.getRandomValues(new Uint8Array(length));
  return Array.from(values, (value) => alphabet[value % alphabet.length]).join("");
}

async function spotifyCodeChallenge(verifier) {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(verifier));
  return btoa(String.fromCharCode(...new Uint8Array(digest)))
    .replace(/=/g, "")
    .replace(/\+/g, "-")
    .replace(/\//g, "_");
}

function storeSpotifyAuth(auth) {
  localStorage.setItem(SPOTIFY_AUTH_STORAGE_KEY, JSON.stringify(auth));
}

async function requestSpotifyToken(parameters) {
  const response = await fetch("https://accounts.spotify.com/api/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams(parameters),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.error_description || data.error || "spotify-token-failed");
  return data;
}

async function buildSpotifyAuthorizationUrl(clientId) {
  const verifier = randomSpotifyString();
  const state = randomSpotifyString(32);
  const challenge = await spotifyCodeChallenge(verifier);
  localStorage.setItem(SPOTIFY_VERIFIER_STORAGE_KEY, verifier);
  localStorage.setItem(SPOTIFY_STATE_STORAGE_KEY, state);
  const authorizationUrl = new URL("https://accounts.spotify.com/authorize");
  authorizationUrl.search = new URLSearchParams({
    client_id: clientId,
    response_type: "code",
    redirect_uri: spotifyRedirectUri(),
    scope: "user-read-currently-playing user-read-playback-state",
    code_challenge_method: "S256",
    code_challenge: challenge,
    state,
  }).toString();
  return authorizationUrl.toString();
}

function readMediaSource() {
  try {
    return localStorage.getItem(MEDIA_SOURCE_STORAGE_KEY) === "local" ? "local" : "spotify";
  } catch {
    return "spotify";
  }
}

function openLocalMediaDatabase() {
  return new Promise((resolve, reject) => {
    if (!window.indexedDB) {
      reject(new Error("indexeddb-unavailable"));
      return;
    }
    const request = window.indexedDB.open(LOCAL_MEDIA_DB_NAME, 1);
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(LOCAL_MEDIA_DB_STORE)) {
        request.result.createObjectStore(LOCAL_MEDIA_DB_STORE);
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function storeLocalMediaHandle(handle) {
  const database = await openLocalMediaDatabase();
  await new Promise((resolve, reject) => {
    const transaction = database.transaction(LOCAL_MEDIA_DB_STORE, "readwrite");
    transaction.objectStore(LOCAL_MEDIA_DB_STORE).put(handle, LOCAL_MEDIA_HANDLE_KEY);
    transaction.oncomplete = resolve;
    transaction.onerror = () => reject(transaction.error);
  });
  database.close();
}

async function readLocalMediaHandle() {
  const database = await openLocalMediaDatabase();
  const handle = await new Promise((resolve, reject) => {
    const transaction = database.transaction(LOCAL_MEDIA_DB_STORE, "readonly");
    const request = transaction.objectStore(LOCAL_MEDIA_DB_STORE).get(LOCAL_MEDIA_HANDLE_KEY);
    request.onsuccess = () => resolve(request.result || null);
    request.onerror = () => reject(request.error);
  });
  database.close();
  return handle;
}

async function collectLocalAudioFiles(directoryHandle, path = "") {
  const files = [];
  for await (const entry of directoryHandle.values()) {
    if (entry.kind === "file" && LOCAL_AUDIO_PATTERN.test(entry.name)) {
      files.push({ file: await entry.getFile(), path });
    } else if (entry.kind === "directory") {
      files.push(...await collectLocalAudioFiles(entry, `${path}${entry.name} / `));
    }
  }
  return files;
}

function decodeId3Text(bytes, encoding = 3) {
  if (!bytes.length) return "";
  try {
    if (encoding === 0) return new TextDecoder("iso-8859-1").decode(bytes).replace(/\0/g, "").trim();
    if (encoding === 1) {
      const littleEndian = bytes[0] === 0xff && bytes[1] === 0xfe;
      const offset = (bytes[0] === 0xff && bytes[1] === 0xfe) || (bytes[0] === 0xfe && bytes[1] === 0xff) ? 2 : 0;
      return new TextDecoder(littleEndian ? "utf-16le" : "utf-16be").decode(bytes.slice(offset)).replace(/\0/g, "").trim();
    }
    if (encoding === 2) return new TextDecoder("utf-16be").decode(bytes).replace(/\0/g, "").trim();
    return new TextDecoder("utf-8").decode(bytes).replace(/\0/g, "").trim();
  } catch {
    return "";
  }
}

function id3FrameSize(bytes, offset, version) {
  if (version === 4) return ((bytes[offset] & 0x7f) << 21) | ((bytes[offset + 1] & 0x7f) << 14) | ((bytes[offset + 2] & 0x7f) << 7) | (bytes[offset + 3] & 0x7f);
  return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
}

async function readLocalAudioMetadata(file, fallbackArtist = "本地音乐") {
  const fallbackTitle = file.name.replace(/\.[^.]+$/, "");
  const metadata = { title: fallbackTitle, artist: fallbackArtist, album: "", artwork: "" };
  if (!/\.mp3$/i.test(file.name)) return metadata;
  try {
    const bytes = new Uint8Array(await file.slice(0, Math.min(file.size, 1024 * 1024)).arrayBuffer());
    if (String.fromCharCode(...bytes.slice(0, 3)) !== "ID3") return metadata;
    const version = bytes[3];
    const tagSize = ((bytes[6] & 0x7f) << 21) | ((bytes[7] & 0x7f) << 14) | ((bytes[8] & 0x7f) << 7) | (bytes[9] & 0x7f);
    let offset = 10;
    const limit = Math.min(bytes.length, 10 + tagSize);
    while (offset + 10 <= limit) {
      const frameId = String.fromCharCode(...bytes.slice(offset, offset + 4));
      if (!/^[A-Z0-9]{4}$/.test(frameId)) break;
      const frameSize = id3FrameSize(bytes, offset + 4, version);
      if (frameSize <= 0 || offset + 10 + frameSize > limit) break;
      const frame = bytes.slice(offset + 10, offset + 10 + frameSize);
      if (frameId === "TIT2") metadata.title = decodeId3Text(frame.slice(1), frame[0]) || metadata.title;
      if (frameId === "TPE1") metadata.artist = decodeId3Text(frame.slice(1), frame[0]) || metadata.artist;
      if (frameId === "TALB") metadata.album = decodeId3Text(frame.slice(1), frame[0]);
      if (frameId === "APIC" && !metadata.artwork) {
        const encoding = frame[0];
        let cursor = 1;
        while (cursor < frame.length && frame[cursor] !== 0) cursor += 1;
        const mime = new TextDecoder("iso-8859-1").decode(frame.slice(1, cursor)) || "image/jpeg";
        cursor += 2;
        const doubleTerminator = encoding === 1 || encoding === 2;
        while (cursor < frame.length - (doubleTerminator ? 1 : 0)) {
          if (frame[cursor] === 0 && (!doubleTerminator || frame[cursor + 1] === 0)) {
            cursor += doubleTerminator ? 2 : 1;
            break;
          }
          cursor += doubleTerminator ? 2 : 1;
        }
        if (cursor < frame.length) metadata.artwork = URL.createObjectURL(new Blob([frame.slice(cursor)], { type: mime }));
      }
      offset += 10 + frameSize;
    }
  } catch {
    // Filename and folder remain useful when metadata cannot be decoded.
  }
  return metadata;
}

function formatMediaTime(seconds) {
  if (!Number.isFinite(seconds) || seconds < 0) return "0:00";
  const whole = Math.floor(seconds);
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, "0")}`;
}

async function copyTextToClipboard(text) {
  if (navigator.clipboard?.writeText && window.isSecureContext) {
    await navigator.clipboard.writeText(text);
    return;
  }
  const helper = document.createElement("textarea");
  helper.value = text;
  helper.setAttribute("readonly", "");
  helper.style.position = "fixed";
  helper.style.opacity = "0";
  document.body.appendChild(helper);
  helper.select();
  const copied = document.execCommand("copy");
  helper.remove();
  if (!copied) throw new Error("copy-failed");
}

const INITIAL_TASKS = [
  { id: 1, title: "素描头像结构练习", done: false, estimatedPomodoros: null },
  { id: 2, title: "色彩静物小稿", done: false, estimatedPomodoros: null },
  { id: 3, title: "速写人物动态", done: false, estimatedPomodoros: null },
];

const MONET_PRESETS = [
  { id: "forest", name: "青瓷", seed: "#30643B" },
  { id: "coral", name: "珊瑚", seed: "#9B4938" },
  { id: "sky", name: "晴空", seed: "#426785" },
  { id: "olive", name: "橄榄", seed: "#6B6918" },
  { id: "cyan", name: "湖蓝", seed: "#216775" },
  { id: "meadow", name: "草甸", seed: "#4E6B34" },
  { id: "blossom", name: "花信", seed: "#8C5265" },
  { id: "violet", name: "紫藤", seed: "#6F5A82" },
];

const MATERIAL_SCHEMES = [
  { id: "tonalSpot", label: "调性点缀", Scheme: SchemeTonalSpot },
  { id: "fidelity", label: "高保真", Scheme: SchemeFidelity },
  { id: "monochrome", label: "单色", Scheme: SchemeMonochrome },
  { id: "neutral", label: "中性", Scheme: SchemeNeutral },
  { id: "vibrant", label: "活力", Scheme: SchemeVibrant },
  { id: "expressive", label: "表现力", Scheme: SchemeExpressive },
  { id: "content", label: "内容主题", Scheme: SchemeContent },
  { id: "rainbow", label: "彩虹", Scheme: SchemeRainbow },
  { id: "fruitSalad", label: "果缤纷", Scheme: SchemeFruitSalad },
];

function darkenHex(hex, amount = 0.24) {
  const value = hex.replace("#", "");
  if (!/^[0-9a-f]{6}$/i.test(value)) return "#234d2d";
  const channels = [0, 2, 4].map((index) =>
    Math.max(0, Math.round(parseInt(value.slice(index, index + 2), 16) * (1 - amount))),
  );
  return `#${channels.map((channel) => channel.toString(16).padStart(2, "0")).join("")}`;
}

function hexToRgb(hex) {
  const value = hex.replace("#", "");
  if (!/^[0-9a-f]{6}$/i.test(value)) return { r: 48, g: 100, b: 59 };
  return {
    r: parseInt(value.slice(0, 2), 16),
    g: parseInt(value.slice(2, 4), 16),
    b: parseInt(value.slice(4, 6), 16),
  };
}

function rgbToHex({ r, g, b }) {
  return `#${[r, g, b]
    .map((channel) => Math.round(channel).toString(16).padStart(2, "0"))
    .join("")}`;
}

function mixHex(base, tint, tintAmount) {
  const a = hexToRgb(base);
  const b = hexToRgb(tint);
  return rgbToHex({
    r: a.r * (1 - tintAmount) + b.r * tintAmount,
    g: a.g * (1 - tintAmount) + b.g * tintAmount,
    b: a.b * (1 - tintAmount) + b.b * tintAmount,
  });
}

function rgbaFromHex(hex, alpha) {
  const { r, g, b } = hexToRgb(hex);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

function relativeLuminance(hex) {
  const { r, g, b } = hexToRgb(hex);
  const linear = [r, g, b].map((channel) => {
    const value = channel / 255;
    return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  });
  return linear[0] * 0.2126 + linear[1] * 0.7152 + linear[2] * 0.0722;
}

function createMaterialScheme(seed, appearance, variantId = "tonalSpot") {
  const Scheme = MATERIAL_SCHEMES.find((item) => item.id === variantId)?.Scheme || SchemeTonalSpot;
  return new Scheme(Hct.fromInt(argbFromHex(seed)), appearance === "dark", 0);
}

function materialHex(value) {
  return hexFromArgb(value).toUpperCase();
}

function materialPreviewColors(seed, variantId = "tonalSpot") {
  const scheme = createMaterialScheme(seed, "dark", variantId);
  return [
    materialHex(scheme.primary),
    materialHex(scheme.secondaryContainer),
    materialHex(scheme.tertiaryContainer),
    materialHex(scheme.surfaceContainerHighest),
  ];
}

function buildMonetTokens(seed, appearance, variantId = "tonalSpot", pureBlack = false) {
  const isLight = appearance === "light";
  const scheme = createMaterialScheme(seed, appearance, variantId);
  const primary = materialHex(scheme.primary);
  const primaryContainer = materialHex(scheme.primaryContainer);
  const surface = materialHex(scheme.surface);
  const surfaceContainer = materialHex(scheme.surfaceContainer);
  const surfaceRaised = materialHex(scheme.surfaceContainerHigh);
  const text = materialHex(scheme.onSurface);
  const muted = materialHex(scheme.onSurfaceVariant);
  const softText = materialHex(scheme.outline);
  const modalBase = materialHex(scheme.surfaceContainerLow);
  const modalPanel = materialHex(scheme.surfaceContainerHigh);
  const usePureBlack = pureBlack && !isLight;
  return {
    "--accent": primary,
    "--accent-deep": primaryContainer,
    "--bg": usePureBlack ? "#000000" : surface,
    "--surface": usePureBlack ? mixHex("#050505", primary, 0.055) : surfaceContainer,
    "--surface-raised": usePureBlack ? mixHex("#0B0B0B", primary, 0.075) : surfaceRaised,
    "--text": text,
    "--muted": muted,
    "--soft-text": softText,
    "--line": rgbaFromHex(materialHex(scheme.outlineVariant), isLight ? 0.66 : 0.74),
    "--line-strong": rgbaFromHex(materialHex(scheme.outline), isLight ? 0.78 : 0.82),
    "--line-soft": rgbaFromHex(materialHex(scheme.outlineVariant), 0.38),
    "--track": materialHex(scheme.surfaceContainerHighest),
    "--switch-bg": materialHex(scheme.surfaceVariant),
    "--switch-border": materialHex(scheme.outline),
    "--shadow": isLight ? rgbaFromHex(materialHex(scheme.shadow), 0.18) : rgbaFromHex(materialHex(scheme.shadow), 0.44),
    "--on-accent": materialHex(scheme.onPrimary),
    "--modal-bg": rgbaFromHex(usePureBlack ? "#050505" : modalBase, isLight ? 0.92 : 0.94),
    "--modal-panel": rgbaFromHex(usePureBlack ? "#0A0A0A" : modalPanel, isLight ? 0.78 : 0.76),
    "--modal-panel-strong": rgbaFromHex(usePureBlack ? "#101010" : materialHex(scheme.surfaceContainerHighest), isLight ? 0.9 : 0.86),
    "--modal-line": rgbaFromHex(materialHex(scheme.outlineVariant), isLight ? 0.58 : 0.68),
    "--modal-shadow": isLight ? rgbaFromHex(materialHex(scheme.shadow), 0.26) : "rgba(0, 0, 0, 0.58)",
  };
}

function describeWeatherCode(code) {
  if (code === 0) return "晴";
  if ([1, 2].includes(code)) return "晴间多云";
  if (code === 3) return "阴";
  if ([45, 48].includes(code)) return "有雾";
  if ([51, 53, 55, 56, 57].includes(code)) return "毛毛雨";
  if ([61, 63, 65, 66, 67].includes(code)) return "有雨";
  if ([71, 73, 75, 77, 85, 86].includes(code)) return "有雪";
  if ([80, 81, 82].includes(code)) return "阵雨";
  if ([95, 96, 99].includes(code)) return "雷雨";
  return "天气变化中";
}

function parseTaskEstimate(value) {
  if (value === "" || value == null) return null;
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? Math.min(99, Math.max(1, parsed)) : null;
}

function clampTaskEstimateInput(value) {
  if (value === "") return "";
  const parsed = Number.parseInt(value, 10);
  if (!Number.isFinite(parsed)) return "";
  return String(Math.min(99, Math.max(1, parsed)));
}

function normalizeTasks(tasks) {
  if (!Array.isArray(tasks)) return [];
  return tasks.map((task) => ({
    ...task,
    estimatedPomodoros: parseTaskEstimate(task?.estimatedPomodoros),
  }));
}

function readStoredTasks() {
  try {
    const saved = JSON.parse(localStorage.getItem("moke-tasks"));
    return Array.isArray(saved) ? normalizeTasks(saved) : normalizeTasks(INITIAL_TASKS);
  } catch {
    return normalizeTasks(INITIAL_TASKS);
  }
}

function readTodayStats() {
  const today = new Date().toLocaleDateString("zh-CN");
  try {
    const saved = JSON.parse(localStorage.getItem("moke-stats"));
    return saved?.date === today ? saved : { date: today, sessions: 0, minutes: 0 };
  } catch {
    return { date: today, sessions: 0, minutes: 0 };
  }
}

function readAppearance(key, fallback) {
  try {
    return localStorage.getItem(key) || fallback;
  } catch {
    return fallback;
  }
}

function readActiveTaskId() {
  try {
    const saved = JSON.parse(localStorage.getItem(ACTIVE_TASK_STORAGE_KEY));
    return saved ?? readStoredTasks()[0]?.id ?? null;
  } catch {
    return readStoredTasks()[0]?.id ?? null;
  }
}

function readInitialWorkspaceView() {
  if (typeof window === "undefined") return "dashboard";
  const view = new URLSearchParams(window.location.search).get("view");
  return ["config", "personalization", "general", "archive", "tools"].includes(view) ? view : "dashboard";
}

function readQuoteMode() {
  return readAppearance(QUOTE_MODE_STORAGE_KEY, "false") === "true";
}

function readQuickNoteLauncherVisible() {
  return readAppearance(QUICK_NOTE_LAUNCHER_STORAGE_KEY, "true") !== "false";
}

function readTranslatorLauncherVisible() {
  return readAppearance(TRANSLATOR_LAUNCHER_STORAGE_KEY, "false") === "true";
}

function readCalculatorLauncherVisible() {
  return readAppearance(CALCULATOR_LAUNCHER_STORAGE_KEY, "false") === "true";
}

function runBasicCalculation(left, operator, right) {
  if (operator === "+") return left + right;
  if (operator === "−") return left - right;
  if (operator === "×") return left * right;
  if (operator === "÷") return right === 0 ? NaN : left / right;
  return right;
}

function formatCalculatorValue(value) {
  if (!Number.isFinite(value)) return "错误";
  const magnitude = Math.abs(value);
  if ((magnitude >= 1e12) || (magnitude > 0 && magnitude < 1e-8)) return value.toExponential(7).replace("e+", "e");
  return Number.parseFloat(value.toPrecision(12)).toString();
}

function readContextLayout() {
  const fallback = {
    order: ["date", "weather", "countdown", "calendar", "media"],
    visible: { date: true, weather: true, countdown: true, calendar: true, media: true },
  };
  try {
    const saved = JSON.parse(localStorage.getItem("moke-context-layout"));
    const savedOrder = Array.isArray(saved?.order)
      ? saved.order.filter((item) => Object.hasOwn(CONTEXT_COMPONENTS, item))
      : [];
    const order = savedOrder.length ? [...new Set(savedOrder)] : [...fallback.order];
    fallback.order.forEach((componentId) => {
      if (order.includes(componentId)) return;
      if (componentId === "countdown") {
        const weatherIndex = order.indexOf("weather");
        order.splice(weatherIndex >= 0 ? weatherIndex + 1 : order.length, 0, componentId);
        return;
      }
      order.push(componentId);
    });
    return {
      order,
      visible: {
        date: saved?.visible?.date !== false,
        weather: saved?.visible?.weather !== false,
        countdown: saved?.visible?.countdown !== false,
        calendar: saved?.visible?.calendar !== false,
        media: saved?.visible?.media !== false,
      },
    };
  } catch {
    return fallback;
  }
}

function localDateInputValue(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function dailyFortuneFor(date) {
  const daySeed = date.getFullYear() * 372 + (date.getMonth() + 1) * 31 + date.getDate();
  return DAILY_FORTUNES[Math.abs(daySeed * 17 + 11) % DAILY_FORTUNES.length];
}

function blobToDataUrl(blob) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result);
    reader.onerror = () => reject(reader.error || new Error("图片读取失败。"));
    reader.readAsDataURL(blob);
  });
}

async function requestPngSaveHandle(suggestedName) {
  if (typeof window.showSaveFilePicker !== "function") return null;
  try {
    return await window.showSaveFilePicker({
      suggestedName,
      types: [{ description: "PNG 图片", accept: { "image/png": [".png"] } }],
      excludeAcceptAllOption: true,
    });
  } catch (error) {
    if (error?.name === "AbortError") return false;
    return null;
  }
}

async function savePngBlob(blob, fileName, saveHandle) {
  if (saveHandle) {
    const writable = await saveHandle.createWritable();
    try {
      await writable.write(blob);
    } finally {
      await writable.close();
    }
    return "picker";
  }

  const url = URL.createObjectURL(blob);
  try {
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.rel = "noopener";
    anchor.style.display = "none";
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    await new Promise((resolve) => window.setTimeout(resolve, 250));
  } finally {
    URL.revokeObjectURL(url);
  }
  return "download";
}

function focusArchiveCareMessage(date) {
  const hour = date.getHours();
  if (hour < 5) return "夜深了，先照顾好自己；未完成的事，也可以留给天亮以后。";
  if (hour < 8) return "早晨很安静，慢慢开始，让第一段专注为今天定下节奏。";
  if (hour < 12) return "上午思路正清晰，稳稳推进眼前最重要的一件事。";
  if (hour < 14) return "中午了，记得好好吃饭和稍作休息，让专注也有余地。";
  if (hour < 18) return "午后容易疲惫，放慢一点也没关系，你仍在认真向前。";
  if (hour < 20) return "傍晚了，收好今天的进展，也给自己留一点喘息。";
  return "晚上适合沉下心，但别忘了为睡眠和明天保留能量。";
}

function defaultCountdown() {
  const target = new Date();
  target.setHours(12, 0, 0, 0);
  target.setDate(target.getDate() + 30);
  return { label: "目标日", date: localDateInputValue(target) };
}

function readCountdown() {
  const fallback = defaultCountdown();
  try {
    const saved = JSON.parse(localStorage.getItem(COUNTDOWN_STORAGE_KEY));
    const label = typeof saved?.label === "string" && saved.label.trim()
      ? saved.label.trim().slice(0, 24)
      : fallback.label;
    const date = /^\d{4}-\d{2}-\d{2}$/.test(saved?.date || "") ? saved.date : fallback.date;
    return { label, date };
  } catch {
    return fallback;
  }
}

function countdownDays(targetDate, now) {
  const [year, month, day] = targetDate.split("-").map(Number);
  const targetUtc = Date.UTC(year, month - 1, day);
  const todayUtc = Date.UTC(now.getFullYear(), now.getMonth(), now.getDate());
  return Math.max(0, Math.ceil((targetUtc - todayUtc) / 86400000));
}

function readThemeMode() {
  const saved = readAppearance("moke-theme-mode", readAppearance("moke-theme", "dark"));
  return ["light", "dark", "auto"].includes(saved) ? saved : "dark";
}

function readThemeVariant() {
  const saved = readAppearance("moke-theme-variant", "tonalSpot");
  return MATERIAL_SCHEMES.some((item) => item.id === saved) ? saved : "tonalSpot";
}

function readFontScale() {
  const saved = Number.parseInt(readAppearance("moke-font-scale", "100"), 10);
  return Number.isFinite(saved) ? Math.min(125, Math.max(85, saved)) : 100;
}

function resolveAutomaticTheme(now, sunTimes) {
  if (sunTimes?.sunrise && sunTimes?.sunset) {
    return now >= sunTimes.sunrise && now < sunTimes.sunset ? "light" : "dark";
  }
  const hour = now.getHours();
  return hour >= 6 && hour < 18 ? "light" : "dark";
}

function readTimerSettings() {
  const fallback = {
    focus: 25,
    short: 5,
    shortBreakEnabled: true,
    countUpEnabled: false,
  };
  try {
    const saved = JSON.parse(localStorage.getItem("moke-timer-settings"));
    return saved ? { ...fallback, ...saved } : fallback;
  } catch {
    return fallback;
  }
}

function readAnalogClockSettings() {
  const fallback = { showMarkers: true, showSeconds: true };
  try {
    const saved = JSON.parse(localStorage.getItem(ANALOG_CLOCK_STORAGE_KEY));
    return saved ? {
      showMarkers: saved.showMarkers !== false,
      showSeconds: saved.showSeconds !== false,
    } : fallback;
  } catch {
    return fallback;
  }
}

function readFocusLogs() {
  try {
    return JSON.parse(localStorage.getItem("moke-focus-logs")) || [];
  } catch {
    return [];
  }
}

function readTaskLists() {
  try {
    const saved = JSON.parse(localStorage.getItem("moke-task-lists"));
    if (Array.isArray(saved) && saved.length) {
      return saved.map((list) => ({
        ...list,
        name: list.id === "today" && list.name === "今日清单" ? "今日任务" : list.name,
        tasks: normalizeTasks(list.tasks),
      }));
    }
  } catch {
    // Fall through to the first local list.
  }
  return [
    { id: "today", name: "今日任务", tasks: readStoredTasks() },
    {
      id: "foundation",
      name: "央美基础训练",
      tasks: [
        { id: 101, title: "素描头像结构练习", done: false, estimatedPomodoros: null },
        { id: 102, title: "色彩静物小稿", done: false, estimatedPomodoros: null },
        { id: 103, title: "速写人物动态", done: false, estimatedPomodoros: null },
      ],
    },
  ];
}

function toDateKey(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function monthCells(date) {
  const first = new Date(date.getFullYear(), date.getMonth(), 1);
  const offset = (first.getDay() + 6) % 7;
  const start = new Date(date.getFullYear(), date.getMonth(), 1 - offset);
  return Array.from({ length: 42 }, (_, index) => {
    const day = new Date(start);
    day.setDate(start.getDate() + index);
    return day;
  });
}

function formatMinutes(seconds) {
  if (!seconds) return "0 分钟";
  if (seconds < 60) return `${seconds} 秒`;
  const minutes = Math.floor(seconds / 60);
  const remainder = seconds % 60;
  return remainder ? `${minutes} 分 ${remainder} 秒` : `${minutes} 分钟`;
}

function startOfWeek(date) {
  const start = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  start.setDate(start.getDate() - ((start.getDay() + 6) % 7));
  return start;
}

function focusSeries(logs, granularity, cursor) {
  const secondsByKey = logs.reduce((map, log) => {
    map.set(log.date, (map.get(log.date) || 0) + log.seconds);
    return map;
  }, new Map());
  const minutes = (seconds) => Math.round((seconds / 60) * 10) / 10;

  if (granularity === "day") {
    const key = toDateKey(cursor);
    const points = Array.from({ length: 24 }, (_, hour) => ({
      key: `${key}-${hour}`,
      label: `${String(hour).padStart(2, "0")}:00`,
      shortLabel: hour % 4 === 0 ? `${String(hour).padStart(2, "0")}:00` : "",
      seconds: 0,
    }));
    logs.filter((log) => log.date === key).forEach((log) => {
      const hour = Math.min(23, Math.max(0, Number.parseInt(log.time?.slice(0, 2), 10) || 0));
      points[hour].seconds += log.seconds;
    });
    return {
      title: cursor.toLocaleDateString("zh-CN", { year: "numeric", month: "long", day: "numeric" }),
      points: points.map((point) => ({ ...point, value: minutes(point.seconds) })),
    };
  }

  if (granularity === "week") {
    const start = startOfWeek(cursor);
    const points = Array.from({ length: 7 }, (_, index) => {
      const day = new Date(start);
      day.setDate(start.getDate() + index);
      const key = toDateKey(day);
      return {
        key,
        label: `${["周一", "周二", "周三", "周四", "周五", "周六", "周日"][index]} ${day.getMonth() + 1}/${day.getDate()}`,
        shortLabel: `${day.getMonth() + 1}/${day.getDate()}`,
        value: minutes(secondsByKey.get(key) || 0),
      };
    });
    const end = new Date(start);
    end.setDate(start.getDate() + 6);
    return { title: `${start.getMonth() + 1}月${start.getDate()}日 — ${end.getMonth() + 1}月${end.getDate()}日`, points };
  }

  if (granularity === "year") {
    const year = cursor.getFullYear();
    return {
      title: `${year} 年`,
      points: Array.from({ length: 12 }, (_, month) => {
        const prefix = `${year}-${String(month + 1).padStart(2, "0")}`;
        const seconds = logs.filter((log) => log.date.startsWith(prefix)).reduce((sum, log) => sum + log.seconds, 0);
        return { key: prefix, label: `${month + 1}月`, shortLabel: `${month + 1}月`, value: minutes(seconds) };
      }),
    };
  }

  const year = cursor.getFullYear();
  const month = cursor.getMonth();
  const dayCount = new Date(year, month + 1, 0).getDate();
  return {
    title: `${year} 年 ${month + 1} 月`,
    points: Array.from({ length: dayCount }, (_, index) => {
      const day = index + 1;
      const key = `${year}-${String(month + 1).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
      return {
        key,
        label: `${month + 1}月${day}日`,
        shortLabel: day === 1 || day === dayCount || day % 5 === 0 ? `${day}日` : "",
        value: minutes(secondsByKey.get(key) || 0),
      };
    }),
  };
}

function shiftStatsCursor(cursor, granularity, direction) {
  const next = new Date(cursor);
  if (granularity === "day") next.setDate(next.getDate() + direction);
  if (granularity === "week") next.setDate(next.getDate() + direction * 7);
  if (granularity === "month") next.setMonth(next.getMonth() + direction, 1);
  if (granularity === "year") next.setFullYear(next.getFullYear() + direction, 0, 1);
  return next;
}

function niceFocusScale(maxValue) {
  const maximum = Math.max(1, maxValue);
  const preferredSteps = [1, 2, 5, 10, 15, 30, 60, 120, 180, 240, 360, 480, 600, 720, 960, 1440];
  const targetStep = maximum / 4;
  let step = preferredSteps.find((value) => value >= targetStep);
  if (!step) step = Math.ceil(targetStep / 1440) * 1440;
  const ceiling = Math.max(step, Math.ceil(maximum / step) * step);
  const ticks = Array.from({ length: Math.round(ceiling / step) + 1 }, (_, index) => index * step);
  return { ceiling, ticks };
}

// Keep every archive surface on the same five-step focus scale.  The calendar
// uses the daily total while a log row uses its own duration, but the visual
// language stays consistent between the two views.
function focusIntensityLevel(seconds) {
  if (!seconds || seconds <= 0) return 0;
  const minutes = seconds / 60;
  if (minutes <= 50) return 1;
  if (minutes <= 100) return 2;
  if (minutes <= 150) return 3;
  if (minutes <= 200) return 4;
  return 5;
}

function focusHeatmap(logs, year) {
  const secondsByDate = logs.reduce((map, log) => {
    map.set(log.date, (map.get(log.date) || 0) + log.seconds);
    return map;
  }, new Map());
  const firstDay = new Date(year, 0, 1);
  const firstMondayOffset = (firstDay.getDay() + 6) % 7;
  const gridStart = new Date(year, 0, 1 - firstMondayOffset);
  const lastDay = new Date(year, 11, 31);
  const totalDays = Math.ceil((lastDay - gridStart) / 86400000) + 1;
  const weeks = Math.ceil(totalDays / 7);
  const cells = Array.from({ length: weeks * 7 }, (_, index) => {
    const date = new Date(gridStart);
    date.setDate(gridStart.getDate() + index);
    const key = toDateKey(date);
    return {
      key,
      date,
      seconds: secondsByDate.get(key) || 0,
      week: Math.floor(index / 7),
      weekday: index % 7,
      inYear: date.getFullYear() === year,
    };
  });
  const months = Array.from({ length: 12 }, (_, month) => {
    const date = new Date(year, month, 1);
    return { label: `${month + 1}月`, week: Math.floor((date - gridStart) / 86400000 / 7) };
  });
  return {
    cells: cells.map((cell) => ({ ...cell, level: focusIntensityLevel(cell.seconds) })),
    months,
    weeks,
    totalSeconds: cells.filter((cell) => cell.inYear).reduce((sum, cell) => sum + cell.seconds, 0),
  };
}

async function sendSystemNotification(title, body, requestPermission = false) {
  if (!("Notification" in window)) return;
  try {
    let permission = Notification.permission;
    if (permission === "default" && requestPermission) {
      permission = await Notification.requestPermission();
    }
    if (permission === "granted") {
      new Notification(title, { body, silent: false });
    }
  } catch {
    // The in-app toast remains the reliable fallback.
  }
}

function formatTime(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return [minutes, seconds].map((value) => String(value).padStart(2, "0"));
}

function playChime() {
  const AudioContext = window.AudioContext || window.webkitAudioContext;
  if (!AudioContext) return;
  const context = new AudioContext();
  [0, 0.22].forEach((delay, index) => {
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    oscillator.type = "sine";
    oscillator.frequency.value = index === 0 ? 660 : 880;
    gain.gain.setValueAtTime(0.0001, context.currentTime + delay);
    gain.gain.exponentialRampToValueAtTime(0.13, context.currentTime + delay + 0.025);
    gain.gain.exponentialRampToValueAtTime(0.0001, context.currentTime + delay + 0.42);
    oscillator.connect(gain).connect(context.destination);
    oscillator.start(context.currentTime + delay);
    oscillator.stop(context.currentTime + delay + 0.44);
  });
  window.setTimeout(() => context.close(), 900);
}

function playTaskCompletionSound() {
  const AudioContext = window.AudioContext || window.webkitAudioContext;
  if (!AudioContext) return;

  try {
    const context = new AudioContext();
    const master = context.createGain();
    master.gain.setValueAtTime(0.0001, context.currentTime);
    master.gain.exponentialRampToValueAtTime(0.085, context.currentTime + 0.018);
    master.gain.exponentialRampToValueAtTime(0.0001, context.currentTime + 0.62);
    master.connect(context.destination);

    [523.25, 783.99].forEach((frequency, index) => {
      const oscillator = context.createOscillator();
      const voice = context.createGain();
      const start = context.currentTime + index * 0.095;
      oscillator.type = index === 0 ? "sine" : "triangle";
      oscillator.frequency.setValueAtTime(frequency, start);
      voice.gain.setValueAtTime(index === 0 ? 0.7 : 0.42, start);
      voice.gain.exponentialRampToValueAtTime(0.0001, start + 0.42);
      oscillator.connect(voice).connect(master);
      oscillator.start(start);
      oscillator.stop(start + 0.44);
    });

    window.setTimeout(() => context.close(), 760);
  } catch {
    // Completing the task remains reliable when audio is unavailable.
  }
}

function useDrawerPresence(open, exitDuration = 210) {
  const [mounted, setMounted] = useState(open);
  const [closing, setClosing] = useState(false);

  useEffect(() => {
    if (open) {
      setMounted(true);
      setClosing(false);
      return undefined;
    }
    if (!mounted) return undefined;

    setClosing(true);
    const timer = window.setTimeout(() => {
      setMounted(false);
      setClosing(false);
    }, exitDuration);
    return () => window.clearTimeout(timer);
  }, [exitDuration, mounted, open]);

  return { mounted, closing };
}

export function App() {
  const [timerSettings, setTimerSettings] = useState(readTimerSettings);
  const [mode, setMode] = useState("focus");
  const [secondsLeft, setSecondsLeft] = useState(() => {
    const settings = readTimerSettings();
    return settings.countUpEnabled ? 0 : settings.focus * 60;
  });
  const [isRunning, setIsRunning] = useState(false);
  const [taskLists, setTaskLists] = useState(readTaskLists);
  const [activeListId, setActiveListId] = useState(() => readAppearance("moke-active-list", "today"));
  const [tasks, setTasks] = useState(() => {
    const lists = readTaskLists();
    const savedId = readAppearance("moke-active-list", "today");
    return lists.find((list) => list.id === savedId)?.tasks || lists[0]?.tasks || readStoredTasks();
  });
  const [listDraftName, setListDraftName] = useState("");
  const [presetTaskDraft, setPresetTaskDraft] = useState("");
  const [presetTaskEstimate, setPresetTaskEstimate] = useState("");
  const [presetDrawerOpen, setPresetDrawerOpen] = useState(false);
  const [editingPresetId, setEditingPresetId] = useState(null);
  const [presetCardMenuId, setPresetCardMenuId] = useState(null);
  const [presetBuilder, setPresetBuilder] = useState({ name: "", tasks: [] });
  const [presetBuilderTask, setPresetBuilderTask] = useState({ title: "", subtitle: "", estimatedPomodoros: "" });
  const [presetSortOpen, setPresetSortOpen] = useState(false);
  const [presetSortDraft, setPresetSortDraft] = useState([]);
  const [draggedPresetId, setDraggedPresetId] = useState(null);
  const [draggedBuilderTaskId, setDraggedBuilderTaskId] = useState(null);
  const [activeTaskId, setActiveTaskId] = useState(readActiveTaskId);
  const [stats, setStats] = useState(readTodayStats);
  const [focusLogs, setFocusLogs] = useState(readFocusLogs);
  const [isAdding, setIsAdding] = useState(false);
  const [newTask, setNewTask] = useState("");
  const [newTaskEstimate, setNewTaskEstimate] = useState("");
  const [immersive, setImmersive] = useState(() => readAppearance(IMMERSIVE_STORAGE_KEY, "false") === "true");
  const [analogClockSettings, setAnalogClockSettings] = useState(readAnalogClockSettings);
  const [railCompact, setRailCompact] = useState(() => readAppearance(RAIL_COMPACT_STORAGE_KEY, "false") === "true");
  const [miniWindowMode, setMiniWindowMode] = useState(false);
  const [quoteMode, setQuoteMode] = useState(readQuoteMode);
  const [quickNoteLauncherVisible, setQuickNoteLauncherVisible] = useState(readQuickNoteLauncherVisible);
  const [translatorLauncherVisible, setTranslatorLauncherVisible] = useState(readTranslatorLauncherVisible);
  const [translatorOpen, setTranslatorOpen] = useState(false);
  const [translatorPosition, setTranslatorPosition] = useState(defaultTranslatorPosition);
  const [calculatorLauncherVisible, setCalculatorLauncherVisible] = useState(readCalculatorLauncherVisible);
  const [calculatorOpen, setCalculatorOpen] = useState(false);
  const [calculatorPosition, setCalculatorPosition] = useState(defaultCalculatorPosition);
  const [calculatorDisplay, setCalculatorDisplay] = useState("0");
  const [calculatorAccumulator, setCalculatorAccumulator] = useState(null);
  const [calculatorOperator, setCalculatorOperator] = useState(null);
  const [calculatorWaiting, setCalculatorWaiting] = useState(false);
  const [calculatorExpression, setCalculatorExpression] = useState("");
  const [stopwatchOpen, setStopwatchOpen] = useState(false);
  const [stopwatchPosition, setStopwatchPosition] = useState(defaultStopwatchPosition);
  const [stopwatchElapsedMs, setStopwatchElapsedMs] = useState(0);
  const [stopwatchRunning, setStopwatchRunning] = useState(false);
  const [stopwatchLaps, setStopwatchLaps] = useState([]);
  const [activeUtilityWindow, setActiveUtilityWindow] = useState(() => readInitialQuickNoteId() ? "note" : null);
  const [quoteIndex, setQuoteIndex] = useState(() => Math.floor(Math.random() * FOCUS_QUOTES.length));
  const [remoteQuote, setRemoteQuote] = useState("");
  const [appearanceMode, setAppearanceMode] = useState(readThemeMode);
  const [theme, setTheme] = useState(() => {
    const mode = readThemeMode();
    return mode === "auto" ? resolveAutomaticTheme(new Date(), null) : mode;
  });
  const [paletteId, setPaletteId] = useState(() => readAppearance("moke-palette", "forest"));
  const [themeVariant, setThemeVariant] = useState(readThemeVariant);
  const [pureBlack, setPureBlack] = useState(() => readAppearance("moke-pure-black", "false") === "true");
  const [fontScaleEnabled, setFontScaleEnabled] = useState(() => readAppearance("moke-font-scale-enabled", "true") !== "false");
  const [fontScale, setFontScale] = useState(readFontScale);
  const [themeSchemeOpen, setThemeSchemeOpen] = useState(false);
  const [customColorOpen, setCustomColorOpen] = useState(false);
  const [customColorDraft, setCustomColorDraft] = useState("#30643B");
  const [settingsOpen, setSettingsOpen] = useState(() => ["config", "personalization", "general"].includes(readInitialWorkspaceView()));
  const [settingsTab, setSettingsTab] = useState(() => {
    const initialView = readInitialWorkspaceView();
    return initialView === "personalization" || initialView === "general" ? initialView : "tasks";
  });
  const [toolsOpen, setToolsOpen] = useState(() => readInitialWorkspaceView() === "tools");
  const [dashboardGridLayout, setDashboardGridLayout] = useState(readDashboardGridLayout);
  const [dashboardEditing, setDashboardEditing] = useState(false);
  const [dashboardAddOpen, setDashboardAddOpen] = useState(false);
  const presetDrawerPresence = useDrawerPresence(presetDrawerOpen);
  const presetSortPresence = useDrawerPresence(presetSortOpen);
  const dashboardAddPresence = useDrawerPresence(dashboardEditing && dashboardAddOpen);
  const [draggingDashboardWidget, setDraggingDashboardWidget] = useState(null);
  const [taskSlide, setTaskSlide] = useState(0);
  const [visibleTaskCount, setVisibleTaskCount] = useState(TASKS_VISIBLE);
  const [dashboardVisible, setDashboardVisible] = useState(() => readInitialWorkspaceView() === "archive");
  const [archiveExporting, setArchiveExporting] = useState(false);
  const [topNotice, setTopNotice] = useState(null);
  const [topNoticeOffset, setTopNoticeOffset] = useState(0);
  const [topNoticeDragging, setTopNoticeDragging] = useState(false);
  const [logoCoolingDown, setLogoCoolingDown] = useState(false);
  const [clearConfirmStep, setClearConfirmStep] = useState(0);
  const [pendingImportBackup, setPendingImportBackup] = useState(null);
  const [backupImportError, setBackupImportError] = useState("");
  const [selectedDate, setSelectedDate] = useState(() => toDateKey(new Date()));
  const [calendarMonth, setCalendarMonth] = useState(() => new Date());
  const [statsGranularity, setStatsGranularity] = useState("month");
  const [statsCursor, setStatsCursor] = useState(() => new Date());
  const [hoveredChartPoint, setHoveredChartPoint] = useState(null);
  const [heatmapYear, setHeatmapYear] = useState(() => new Date().getFullYear());
  const [hoveredHeatmapCell, setHoveredHeatmapCell] = useState(null);
  const [currentTime, setCurrentTime] = useState(() => new Date());
  const [weather, setWeather] = useState({
    status: "loading",
    location: "北京",
    description: "天气加载中",
    temperature: null,
    fallback: true,
  });
  const [weatherRefreshKey, setWeatherRefreshKey] = useState(0);
  const [sunTimes, setSunTimes] = useState(null);
  const [mediaInfo, setMediaInfo] = useState({
    available: false,
    title: "连接 Spotify",
    artist: "",
    artwork: "",
  });
  const [mediaSource, setMediaSource] = useState(readMediaSource);
  const [localTracks, setLocalTracks] = useState([]);
  const [localTrackIndex, setLocalTrackIndex] = useState(0);
  const [localFolderName, setLocalFolderName] = useState("");
  const [localFolderAccess, setLocalFolderAccess] = useState("none");
  const [localImporting, setLocalImporting] = useState(false);
  const [localPlaying, setLocalPlaying] = useState(false);
  const [localCurrentTime, setLocalCurrentTime] = useState(0);
  const [localDuration, setLocalDuration] = useState(0);
  const [localVolume, setLocalVolume] = useState(() => Math.min(1, Math.max(0, Number(readAppearance(LOCAL_VOLUME_STORAGE_KEY, "0.72")) || 0.72)));
  const [localPlaybackMode, setLocalPlaybackMode] = useState(() => {
    const savedMode = readAppearance(LOCAL_PLAYBACK_MODE_STORAGE_KEY, "sequence");
    return ["sequence", "repeat-one", "shuffle"].includes(savedMode) ? savedMode : "sequence";
  });
  const [localMuted, setLocalMuted] = useState(false);
  const [spotifyClientId, setSpotifyClientId] = useState(() => readAppearance(SPOTIFY_CLIENT_ID_STORAGE_KEY, ""));
  const [spotifyAuth, setSpotifyAuth] = useState(readSpotifyAuth);
  const [spotifyStatus, setSpotifyStatus] = useState(() => readSpotifyAuth() ? "connected" : "idle");
  const [contextLayout, setContextLayout] = useState(readContextLayout);
  const [countdown, setCountdown] = useState(readCountdown);
  const [openContextSettings, setOpenContextSettings] = useState(null);
  const [draggedContext, setDraggedContext] = useState(null);
  const [quickNotes, setQuickNotes] = useState(readQuickNotes);
  const [openQuickNoteId, setOpenQuickNoteId] = useState(readInitialQuickNoteId);
  const [quickNotePosition, setQuickNotePosition] = useState(defaultQuickNotePosition);
  const [deletingQuickNoteId, setDeletingQuickNoteId] = useState(null);
  const completedRef = useRef(false);
  const dashboardWidgetDragRef = useRef(null);
  const taskListRef = useRef(null);
  const taskSliderShellRef = useRef(null);
  const committedSecondsRef = useRef(0);
  const topNoticeTimerRef = useRef(null);
  const topNoticeDragRef = useRef(null);
  const logoClickPatternRef = useRef({ clicks: [], messageIndex: 0, lastMessageAt: 0, lockedUntil: 0 });
  const logoCooldownTimerRef = useRef(null);
  const spotifyAuthRef = useRef(spotifyAuth);
  const localAudioRef = useRef(null);
  const localFolderInputRef = useRef(null);
  const backupImportInputRef = useRef(null);
  const localDirectoryHandleRef = useRef(null);
  const localAutoplayRef = useRef(false);
  const localTracksRef = useRef([]);
  const restoredLocalFolderRef = useRef(false);
  const quickNoteDragRef = useRef(null);
  const quickNotePositionRef = useRef(quickNotePosition);
  const quickNoteDeleteTimerRef = useRef(null);
  const translatorDragRef = useRef(null);
  const translatorPositionRef = useRef(translatorPosition);
  const calculatorDragRef = useRef(null);
  const calculatorPositionRef = useRef(calculatorPosition);
  const stopwatchDragRef = useRef(null);
  const stopwatchPositionRef = useRef(stopwatchPosition);
  const stopwatchStartedAtRef = useRef(0);
  const miniWindowRef = useRef(null);
  const toggleTimerRef = useRef(null);
  const taskLongPressRef = useRef(null);
  const taskReorderRef = useRef(null);
  const suppressTaskClickRef = useRef(false);
  const [reorderingTaskId, setReorderingTaskId] = useState(null);

  const duration = timerSettings[mode] * 60;
  const countUpEnabled = timerSettings.countUpEnabled;
  const [minuteText, secondText] = formatTime(secondsLeft);
  const analogSecondAngle = currentTime.getSeconds() * 6;
  const analogMinuteAngle = (currentTime.getMinutes() + currentTime.getSeconds() / 60) * 6;
  const analogHourAngle = (
    (currentTime.getHours() % 12)
    + currentTime.getMinutes() / 60
    + currentTime.getSeconds() / 3600
  ) * 30;
  const immersiveClockParts = [
    { key: "hours", value: String(Math.floor(secondsLeft / 3600)).padStart(2, "0"), label: "小时" },
    { key: "minutes", value: String(Math.floor((secondsLeft % 3600) / 60)).padStart(2, "0"), label: "分钟" },
    { key: "seconds", value: String(secondsLeft % 60).padStart(2, "0"), label: "秒" },
  ];
  const immersivePhase = !isRunning ? "paused" : countUpEnabled || mode === "focus" ? "focus" : "short";
  const immersivePhaseLabel = immersivePhase === "paused" ? "暂停中" : immersivePhase === "short" ? "短休中" : "专注中";
  const progress = countUpEnabled ? 0 : duration ? 1 - secondsLeft / duration : 0;
  const elapsedSeconds = countUpEnabled ? secondsLeft : Math.max(0, duration - secondsLeft);
  const activeList = taskLists.find((list) => list.id === activeListId) || taskLists[0];
  const activeListName = activeList?.name?.trim() || "今日任务";
  const activeTask = tasks.find((task) => task.id === activeTaskId) || tasks[0];
  const completedPomodorosByTask = useMemo(() => focusLogs.reduce((counts, log) => {
    if (log.reason !== "completed" || log.taskId == null) return counts;
    counts.set(log.taskId, (counts.get(log.taskId) || 0) + 1);
    return counts;
  }, new Map()), [focusLogs]);
  const activeQuickNote = quickNotes.find((note) => note.id === openQuickNoteId) || null;
  const currentLocalTrack = localTracks[localTrackIndex] || null;
  const displayedMedia = mediaSource === "local"
    ? {
        available: Boolean(currentLocalTrack),
        title: currentLocalTrack?.title || "导入本地音乐",
        artist: currentLocalTrack?.artist || (localFolderName
          ? `${localFolderName} · ${localFolderAccess === "restoring" ? "正在恢复" : localFolderAccess === "permission" ? "等待确认读取" : "等待读取"}`
          : "选择一个音乐文件夹"),
        artwork: currentLocalTrack?.artwork || "",
      }
    : mediaInfo;
  const sortedQuickNotes = useMemo(
    () => [...quickNotes].sort((a, b) => new Date(b.updatedAt) - new Date(a.updatedAt)),
    [quickNotes],
  );
  const matchedPalette = MONET_PRESETS.find((preset) => preset.id === paletteId);
  const customSeed = /^#[0-9a-f]{6}$/i.test(paletteId) ? paletteId.toUpperCase() : null;
  const activePalette = matchedPalette || {
    id: "custom",
    name: "自定义",
    seed: customSeed || MONET_PRESETS[0].seed,
  };
  const monetTokens = useMemo(
    () => buildMonetTokens(activePalette.seed, theme, themeVariant, pureBlack),
    [activePalette.seed, pureBlack, theme, themeVariant],
  );
  const activeThemeVariant = MATERIAL_SCHEMES.find((item) => item.id === themeVariant) || MATERIAL_SCHEMES[0];
  const todayKey = toDateKey(new Date());
  const calendarDays = monthCells(calendarMonth);
  const selectedLogs = focusLogs.filter((log) => log.date === selectedDate);
  const todayLogs = focusLogs.filter((log) => log.date === todayKey);
  const todayTrackedSeconds = todayLogs.reduce((sum, log) => sum + log.seconds, 0);
  const selectedTrackedSeconds = selectedLogs.reduce((sum, log) => sum + log.seconds, 0);
  const liveFocusSeconds = isRunning && mode === "focus"
    ? Math.max(0, elapsedSeconds - committedSecondsRef.current)
    : 0;
  const trueTodaySeconds = todayTrackedSeconds + liveFocusSeconds;
  const todayFocusSessions = todayLogs.length + (liveFocusSeconds > 0 ? 1 : 0);
  const completedPomodoros = todayLogs.filter((log) => log.reason === "completed").length;
  const calendarMonthPrefix = `${calendarMonth.getFullYear()}-${String(calendarMonth.getMonth() + 1).padStart(2, "0")}`;
  const monthlyPomodoros = focusLogs.filter(
    (log) => log.date.startsWith(calendarMonthPrefix) && log.reason === "completed",
  ).length;
  const chartLogs = liveFocusSeconds > 0
    ? [...focusLogs, {
      date: todayKey,
      time: new Date().toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit", hour12: false }),
      seconds: liveFocusSeconds,
    }]
    : focusLogs;
  const focusChart = focusSeries(chartLogs, statsGranularity, statsCursor);
  const focusChartMax = Math.max(0, ...focusChart.points.map((point) => point.value));
  const focusChartScale = niceFocusScale(focusChartMax || 30);
  const focusChartCeiling = focusChartScale.ceiling;
  const focusChartPoints = focusChart.points.map((point, index) => ({
    ...point,
    x: focusChart.points.length === 1 ? 360 : 58 + index * (628 / (focusChart.points.length - 1)),
    y: 214 - (point.value / focusChartCeiling) * 164,
  }));
  const focusChartPath = focusChartPoints.map((point, index) => `${index ? "L" : "M"}${point.x.toFixed(2)},${point.y.toFixed(2)}`).join(" ");
  const heatmap = focusHeatmap(chartLogs, heatmapYear);
  const maxTaskSlide = Math.max(tasks.length - visibleTaskCount, 0);
  const dateCopy = useMemo(() => {
    const now = currentTime;
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    return {
      numeric: `${month} / ${day}`,
      weekday: new Intl.DateTimeFormat("zh-CN", { weekday: "short" }).format(now),
    };
  }, [currentTime]);
  const contextCalendarDays = monthCells(currentTime);
  const contextCalendarMonth = currentTime.toLocaleDateString("zh-CN", {
    year: "numeric",
    month: "long",
  });
  const clockCopy = currentTime
    .toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit", hour12: false })
    .replace(":", "：");
  const countdownLabel = countdown.label.trim() || "目标日";
  const countdownRemainingDays = countdownDays(countdown.date, currentTime);
  const countdownCopy = `距离${countdownLabel}还有${countdownRemainingDays}天`;
  const archiveCareMessage = focusArchiveCareMessage(currentTime);
  const spotifyNeedsLoopback = typeof window !== "undefined" && window.location.hostname === "localhost";
  const spotifyRedirect = spotifyNeedsLoopback ? spotifyLoopbackUrl() : spotifyRedirectUri();

  const syncWorkspaceUrl = (view) => {
    const url = new URL(window.location.href);
    if (view === "dashboard") url.searchParams.delete("view");
    else url.searchParams.set("view", view);
    window.history.replaceState(null, "", `${url.pathname}${url.search}${url.hash}`);
  };

  const scrollWorkspaceToTop = (view, behavior = "smooth") => {
    const selectors = {
      dashboard: ".dashboard-grid-workspace",
      config: ".settings-popover",
      personalization: ".settings-popover",
      general: ".settings-popover",
      archive: ".archive-workspace .dashboard-scroll-body",
      tools: ".workspace-page-shell.tools-material-shell",
    };
    const scrollSurface = document.querySelector(selectors[view]);
    if (!scrollSurface) return;
    scrollSurface.scrollTo({ top: 0, behavior });
  };

  const openDashboard = () => {
    setDashboardVisible(true);
    syncWorkspaceUrl("archive");
  };

  const closeDashboard = () => {
    setClearConfirmStep(0);
    setDashboardVisible(false);
    syncWorkspaceUrl("dashboard");
  };

  const leaveDashboardImmediately = () => {
    setDashboardVisible(false);
    setClearConfirmStep(0);
  };

  const showDashboardView = () => {
    if (!settingsOpen && !dashboardVisible && !toolsOpen) {
      scrollWorkspaceToTop("dashboard");
      return;
    }
    leaveDashboardImmediately();
    setSettingsOpen(false);
    setToolsOpen(false);
    syncWorkspaceUrl("dashboard");
  };

  const showConfigurationView = () => {
    if (settingsOpen && settingsTab === "tasks") {
      scrollWorkspaceToTop("config");
      return;
    }
    leaveDashboardImmediately();
    setToolsOpen(false);
    setSettingsTab("tasks");
    setSettingsOpen(true);
    syncWorkspaceUrl("config");
  };

  const showPersonalizationView = () => {
    if (settingsOpen && settingsTab === "personalization") {
      scrollWorkspaceToTop("personalization");
      return;
    }
    leaveDashboardImmediately();
    setToolsOpen(false);
    setSettingsTab("personalization");
    setSettingsOpen(true);
    syncWorkspaceUrl("personalization");
  };

  const showGeneralView = () => {
    if (settingsOpen && settingsTab === "general") {
      scrollWorkspaceToTop("general");
      return;
    }
    leaveDashboardImmediately();
    setToolsOpen(false);
    setSettingsTab("general");
    setSettingsOpen(true);
    syncWorkspaceUrl("general");
  };

  const showArchiveView = () => {
    if (dashboardVisible) {
      scrollWorkspaceToTop("archive");
      return;
    }
    setSettingsOpen(false);
    setToolsOpen(false);
    openDashboard();
  };

  const showToolsView = () => {
    if (toolsOpen) {
      scrollWorkspaceToTop("tools");
      return;
    }
    leaveDashboardImmediately();
    setSettingsOpen(false);
    setToolsOpen(true);
    syncWorkspaceUrl("tools");
  };

  useEffect(() => {
    const clockTimer = window.setInterval(() => setCurrentTime(new Date()), 1000);
    return () => window.clearInterval(clockTimer);
  }, []);

  useEffect(() => {
    const editableContextTarget = [
      "textarea",
      "[contenteditable]:not([contenteditable='false'])",
      "input:not([type])",
      "input[type='text']",
      "input[type='search']",
      "input[type='url']",
      "input[type='email']",
      "input[type='tel']",
      "input[type='password']",
      "input[type='number']",
    ].join(", ");

    const blockNonTextContextMenu = (event) => {
      if (event.target instanceof Element && event.target.closest(editableContextTarget)) return;
      event.preventDefault();
    };

    const handleAppShortcut = async (event) => {
      if (event.repeat) return;
      if (event.key === "Escape") {
        setImmersive(false);
        return;
      }
      if (event.key === "F5") {
        event.preventDefault();
        setWeatherRefreshKey((current) => current + 1);
        return;
      }
      if (event.key !== "F12") return;
      event.preventDefault();
      try {
        if (document.fullscreenElement) {
          await document.exitFullscreen();
        } else {
          await document.documentElement.requestFullscreen();
        }
      } catch {
        // Fullscreen can be denied by browser or operating-system policy.
      }
    };

    window.addEventListener("contextmenu", blockNonTextContextMenu);
    window.addEventListener("keydown", handleAppShortcut);
    return () => {
      window.removeEventListener("contextmenu", blockNonTextContextMenu);
      window.removeEventListener("keydown", handleAppShortcut);
    };
  }, []);

  useEffect(() => {
    const transferredClientId = new URLSearchParams(window.location.hash.slice(1)).get("spotify_client_id");
    if (!transferredClientId) return;
    localStorage.setItem(SPOTIFY_CLIENT_ID_STORAGE_KEY, transferredClientId);
    setSpotifyClientId(transferredClientId);
    setSpotifyStatus("connecting");
    window.history.replaceState({}, "", `${window.location.pathname}${window.location.search}`);
    buildSpotifyAuthorizationUrl(transferredClientId)
      .then((authorizationUrl) => window.location.assign(authorizationUrl))
      .catch(() => {
        setSettingsOpen(true);
        setSpotifyStatus("error");
      });
  }, []);

  useEffect(() => {
    let cancelled = false;
    let locationVersion = 0;
    let activeLocation = BEIJING_WEATHER_LOCATION;

    const loadWeather = async (location, version) => {
      try {
        const query = new URLSearchParams({
          latitude: String(location.latitude),
          longitude: String(location.longitude),
          current: "temperature_2m,apparent_temperature,weather_code,wind_speed_10m",
          daily: "sunrise,sunset",
          forecast_days: "1",
          timezone: "auto",
        });
        const response = await fetch(`https://api.open-meteo.com/v1/forecast?${query}`);
        if (!response.ok) throw new Error("weather-request-failed");
        const data = await response.json();
        if (cancelled || version !== locationVersion) return;
        setWeather({
          status: "ready",
          location: location.label,
          description: describeWeatherCode(data.current?.weather_code),
          temperature: Math.round(data.current?.temperature_2m),
          apparentTemperature: Math.round(data.current?.apparent_temperature),
          fallback: location.fallback,
        });
        const sunrise = data.daily?.sunrise?.[0] ? new Date(data.daily.sunrise[0]) : null;
        const sunset = data.daily?.sunset?.[0] ? new Date(data.daily.sunset[0]) : null;
        if (sunrise && sunset && !Number.isNaN(sunrise.getTime()) && !Number.isNaN(sunset.getTime())) {
          setSunTimes({ sunrise, sunset, location: location.label });
        }
      } catch {
        if (cancelled || version !== locationVersion) return;
        if (!location.fallback) {
          activateLocation(BEIJING_WEATHER_LOCATION);
          return;
        }
        setWeather({
          status: "error",
          location: "北京",
          description: "天气暂不可用",
          temperature: null,
          fallback: true,
        });
      }
    };

    const activateLocation = (location) => {
      activeLocation = location;
      locationVersion += 1;
      setWeather((current) => ({
        ...current,
        status: "loading",
        location: location.label,
        fallback: location.fallback,
      }));
      loadWeather(location, locationVersion);
    };

    activateLocation(BEIJING_WEATHER_LOCATION);

    if (navigator.geolocation) {
      navigator.geolocation.getCurrentPosition(
        ({ coords }) => activateLocation({
          latitude: coords.latitude,
          longitude: coords.longitude,
          label: "当前位置",
          fallback: false,
        }),
        () => {},
        { enableHighAccuracy: false, timeout: 7000, maximumAge: 30 * 60 * 1000 },
      );
    }

    const weatherTimer = window.setInterval(() => {
      loadWeather(activeLocation, locationVersion);
    }, 10 * 60 * 1000);

    return () => {
      cancelled = true;
      window.clearInterval(weatherTimer);
    };
  }, [weatherRefreshKey]);

  useEffect(() => {
    if (appearanceMode !== "auto") return;
    setTheme(resolveAutomaticTheme(currentTime, sunTimes));
  }, [appearanceMode, currentTime, sunTimes]);

  useEffect(() => {
    spotifyAuthRef.current = spotifyAuth;
  }, [spotifyAuth]);

  useEffect(() => {
    const parameters = new URLSearchParams(window.location.search);
    const code = parameters.get("code");
    const returnedState = parameters.get("state");
    const spotifyError = parameters.get("error");
    if (!code && !spotifyError) return;

    const cleanCallbackUrl = () => {
      const cleanUrl = new URL(window.location.href);
      ["code", "state", "error"].forEach((key) => cleanUrl.searchParams.delete(key));
      window.history.replaceState({}, "", `${cleanUrl.pathname}${cleanUrl.search}${cleanUrl.hash}`);
    };

    if (spotifyError) {
      cleanCallbackUrl();
      setSpotifyStatus("error");
      showTopMessage("Spotify 未连接", "授权已取消，可以随时从界面设置中重新连接。", false, "warning", false);
      return;
    }

    const verifier = localStorage.getItem(SPOTIFY_VERIFIER_STORAGE_KEY);
    const expectedState = localStorage.getItem(SPOTIFY_STATE_STORAGE_KEY);
    const clientId = localStorage.getItem(SPOTIFY_CLIENT_ID_STORAGE_KEY);
    if (!verifier || !expectedState || expectedState !== returnedState || !clientId) {
      cleanCallbackUrl();
      setSpotifyStatus("error");
      showTopMessage("Spotify 验证失败", "授权状态已失效，请重新连接。", false, "warning", false);
      return;
    }

    let cancelled = false;
    setSpotifyStatus("connecting");
    requestSpotifyToken({
      client_id: clientId,
      grant_type: "authorization_code",
      code,
      redirect_uri: spotifyRedirectUri(),
      code_verifier: verifier,
    })
      .then((token) => {
        if (cancelled) return;
        const nextAuth = {
          accessToken: token.access_token,
          refreshToken: token.refresh_token,
          expiresAt: Date.now() + token.expires_in * 1000,
        };
        storeSpotifyAuth(nextAuth);
        setSpotifyAuth(nextAuth);
        setSpotifyStatus("connected");
        showTopMessage("Spotify 已连接", "当前歌曲会自动显示在右侧。", false, "focus", false);
      })
      .catch(() => {
        if (cancelled) return;
        setSpotifyStatus("error");
        showTopMessage("Spotify 连接失败", "请检查 Client ID 与回调地址是否完全一致。", false, "warning", false);
      })
      .finally(() => {
        localStorage.removeItem(SPOTIFY_VERIFIER_STORAGE_KEY);
        localStorage.removeItem(SPOTIFY_STATE_STORAGE_KEY);
        cleanCallbackUrl();
      });

    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (mediaSource !== "spotify") return undefined;
    if (!spotifyAuth) {
      setMediaInfo({ available: false, title: "连接 Spotify", artist: "", artwork: "" });
      return undefined;
    }

    let cancelled = false;

    const refreshAccessToken = async () => {
      const token = await requestSpotifyToken({
        client_id: spotifyClientId,
        grant_type: "refresh_token",
        refresh_token: spotifyAuthRef.current.refreshToken,
      });
      const nextAuth = {
        accessToken: token.access_token,
        refreshToken: token.refresh_token || spotifyAuthRef.current.refreshToken,
        expiresAt: Date.now() + token.expires_in * 1000,
      };
      storeSpotifyAuth(nextAuth);
      spotifyAuthRef.current = nextAuth;
      if (!cancelled) setSpotifyAuth(nextAuth);
      return nextAuth.accessToken;
    };

    const loadSpotifyMedia = async () => {
      try {
        let auth = spotifyAuthRef.current;
        let accessToken = auth.accessToken;
        if (auth.expiresAt <= Date.now() + 30_000) accessToken = await refreshAccessToken();

        let response = await fetch("https://api.spotify.com/v1/me/player/currently-playing?additional_types=track,episode", {
          headers: { Authorization: `Bearer ${accessToken}` },
        });
        if (response.status === 401) {
          accessToken = await refreshAccessToken();
          response = await fetch("https://api.spotify.com/v1/me/player/currently-playing?additional_types=track,episode", {
            headers: { Authorization: `Bearer ${accessToken}` },
          });
        }
        if (cancelled) return;
        if (response.status === 204) {
          setMediaInfo({ available: false, title: "Spotify 暂无播放", artist: "", artwork: "" });
          setSpotifyStatus("connected");
          return;
        }
        if (!response.ok) throw new Error("spotify-media-failed");
        const data = await response.json();
        const item = data.item;
        const artwork = item?.album?.images?.[0]?.url || item?.images?.[0]?.url || "";
        const artist = item?.artists?.map((entry) => entry.name).filter(Boolean).join("、")
          || item?.show?.publisher
          || item?.show?.name
          || "Spotify";
        setMediaInfo({
          available: Boolean(item),
          title: item?.name || "Spotify 暂无播放",
          artist: item ? artist : "",
          artwork,
        });
        setSpotifyStatus("connected");
      } catch {
        if (cancelled) return;
        setSpotifyStatus("error");
        setMediaInfo({ available: false, title: "Spotify 暂不可用", artist: "", artwork: "" });
      }
    };

    loadSpotifyMedia();
    const mediaTimer = window.setInterval(loadSpotifyMedia, 10_000);
    const refreshWhenVisible = () => {
      if (document.visibilityState === "visible") loadSpotifyMedia();
    };
    document.addEventListener("visibilitychange", refreshWhenVisible);
    return () => {
      cancelled = true;
      window.clearInterval(mediaTimer);
      document.removeEventListener("visibilitychange", refreshWhenVisible);
    };
  }, [mediaSource, spotifyAuth, spotifyClientId]);

  useEffect(() => {
    try {
      localStorage.setItem(MEDIA_SOURCE_STORAGE_KEY, mediaSource);
    } catch {
      // The source selection may remain session-only when storage is restricted.
    }
  }, [mediaSource]);

  useEffect(() => {
    if (mediaSource !== "local" || restoredLocalFolderRef.current || !("showDirectoryPicker" in window)) return;
    restoredLocalFolderRef.current = true;
    let cancelled = false;
    readLocalMediaHandle()
      .then(async (handle) => {
        if (!handle || cancelled) return;
        localDirectoryHandleRef.current = handle;
        setLocalFolderName(handle.name || "本地音乐");
        await restoreSavedLocalMusicFolder({ handle, silent: true, isCancelled: () => cancelled });
      })
      .catch(() => {});
    return () => { cancelled = true; };
  }, [mediaSource]);

  useEffect(() => () => {
    localTracksRef.current.forEach((track) => {
      URL.revokeObjectURL(track.url);
      if (track.artwork?.startsWith("blob:")) URL.revokeObjectURL(track.artwork);
    });
  }, []);

  const commitFocusSegment = (reason = "paused") => {
    if (mode !== "focus") return;
    const elapsed = elapsedSeconds;
    const delta = Math.max(0, elapsed - committedSecondsRef.current);
    if (delta < 1) return;
    const now = new Date();
    setFocusLogs((current) => [
      ...current,
      {
        id: `${now.getTime()}-${current.length}`,
        date: toDateKey(now),
        time: now.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit" }),
        taskId: activeTask?.id ?? null,
        taskTitle: activeTask?.title || "未指定任务",
        seconds: delta,
        reason,
      },
    ]);
    committedSecondsRef.current = elapsed;
  };

  const showTopMessage = (title, body, requestPermission = false, type = "info", allowSystem = true) => {
    window.clearTimeout(topNoticeTimerRef.current);
    setTopNoticeOffset(0);
    setTopNoticeDragging(false);
    setTopNotice({ title, body, type, id: Date.now() });
    topNoticeTimerRef.current = window.setTimeout(() => setTopNotice(null), 5200);
    if (allowSystem) sendSystemNotification(title, body, requestPermission);
  };

  const showDailyFortune = () => {
    const now = Date.now();
    const pattern = logoClickPatternRef.current;
    if (now < pattern.lockedUntil) return;
    pattern.clicks = [...pattern.clicks.filter((timestamp) => now - timestamp < 3000), now];
    if (pattern.clicks.length >= 5 && now - pattern.lastMessageAt > 2200) {
      const message = LOGO_PROCRASTINATION_MESSAGES[pattern.messageIndex % LOGO_PROCRASTINATION_MESSAGES.length];
      pattern.messageIndex += 1;
      pattern.lastMessageAt = now;
      pattern.clicks = [];
      pattern.lockedUntil = now + 5000;
      window.clearTimeout(logoCooldownTimerRef.current);
      setLogoCoolingDown(true);
      logoCooldownTimerRef.current = window.setTimeout(() => {
        logoClickPatternRef.current.clicks = [];
        logoClickPatternRef.current.lockedUntil = 0;
        setLogoCoolingDown(false);
      }, 5000);
      showTopMessage(message.title, message.body, false, "warning", false);
      return;
    }
    const fortune = dailyFortuneFor(new Date());
    showTopMessage(`今日运势 · ${fortune.level}`, fortune.quip, false, "tomato", false);
  };

  const startTopNoticeSwipe = (event) => {
    topNoticeDragRef.current = { pointerId: event.pointerId, startX: event.clientX };
    setTopNoticeDragging(true);
    event.currentTarget.setPointerCapture?.(event.pointerId);
  };

  const moveTopNoticeSwipe = (event) => {
    const drag = topNoticeDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;
    setTopNoticeOffset(event.clientX - drag.startX);
  };

  const finishTopNoticeSwipe = (event) => {
    const drag = topNoticeDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;
    const offset = event.clientX - drag.startX;
    topNoticeDragRef.current = null;
    setTopNoticeDragging(false);
    if (Math.abs(offset) >= 96) {
      setTopNoticeOffset(offset > 0 ? window.innerWidth : -window.innerWidth);
      window.setTimeout(() => setTopNotice(null), 180);
      return;
    }
    setTopNoticeOffset(0);
  };

  const loadLocalTrackFiles = async (entries, folderName = "本地音乐", announce = true) => {
    const sortedEntries = [...entries]
      .filter(({ file }) => file && LOCAL_AUDIO_PATTERN.test(file.name))
      .sort((a, b) => new Intl.Collator("zh-CN", { numeric: true, sensitivity: "base" }).compare(`${a.path}${a.file.name}`, `${b.path}${b.file.name}`));
    if (!sortedEntries.length) {
      showTopMessage("没有找到音乐", "请选择包含 MP3、M4A、WAV、OGG、FLAC 或 Opus 音频的文件夹。", false, "warning", false);
      return;
    }

    setLocalImporting(true);
    try {
      const nextTracks = [];
      for (let index = 0; index < sortedEntries.length; index += 12) {
        const batch = sortedEntries.slice(index, index + 12);
        const tracks = await Promise.all(batch.map(async ({ file, path }) => {
          const metadata = await readLocalAudioMetadata(file, path.replace(/\s*\/\s*$/, "") || folderName);
          return {
            id: `${file.name}-${file.size}-${file.lastModified}-${path}`,
            file,
            url: URL.createObjectURL(file),
            path,
            ...metadata,
          };
        }));
        nextTracks.push(...tracks);
      }

      localTracksRef.current.forEach((track) => {
        URL.revokeObjectURL(track.url);
        if (track.artwork?.startsWith("blob:")) URL.revokeObjectURL(track.artwork);
      });
      localTracksRef.current = nextTracks;
      setLocalTracks(nextTracks);
      setLocalTrackIndex(0);
      setLocalFolderName(folderName);
      setLocalCurrentTime(0);
      setLocalDuration(0);
      setLocalPlaying(false);
      setLocalFolderAccess("ready");
      if (announce) showTopMessage("本地音乐已导入", `已读取“${folderName}”中的 ${nextTracks.length} 首音乐。`, false, "focus", false);
    } finally {
      setLocalImporting(false);
    }
  };

  const restoreSavedLocalMusicFolder = async ({ handle = localDirectoryHandleRef.current, silent = false, isCancelled = () => false } = {}) => {
    if (!handle) return false;
    localDirectoryHandleRef.current = handle;
    setLocalFolderName(handle.name || "本地音乐");
    setLocalFolderAccess("restoring");
    setLocalImporting(true);
    try {
      let permission = handle.queryPermission ? await handle.queryPermission({ mode: "read" }) : "granted";
      if (permission !== "granted" && handle.requestPermission) {
        try {
          permission = await handle.requestPermission({ mode: "read" });
        } catch {
          permission = "prompt";
        }
      }
      if (isCancelled()) return false;
      if (permission !== "granted") {
        setLocalFolderAccess("permission");
        if (!silent) showTopMessage("需要确认文件夹访问", "点击“恢复读取”并允许访问，无需重新导入音乐。", false, "warning", false);
        return false;
      }
      const entries = await collectLocalAudioFiles(handle);
      if (isCancelled()) return false;
      await loadLocalTrackFiles(entries, handle.name || "本地音乐", !silent);
      return true;
    } catch {
      if (!isCancelled()) {
        setLocalFolderAccess("permission");
        if (!silent) showTopMessage("无法恢复音乐库", "请确认原文件夹仍然存在，或更换音乐文件夹。", false, "warning", false);
      }
      return false;
    } finally {
      if (!isCancelled()) setLocalImporting(false);
    }
  };

  const importLocalMusicFolder = async () => {
    if ("showDirectoryPicker" in window) {
      try {
        const handle = await window.showDirectoryPicker({ id: "tomatotodo-music", mode: "read" });
        localDirectoryHandleRef.current = handle;
        setLocalFolderAccess("restoring");
        const entries = await collectLocalAudioFiles(handle);
        await storeLocalMediaHandle(handle).catch(() => {});
        await loadLocalTrackFiles(entries, handle.name || "本地音乐");
      } catch (error) {
        if (error?.name !== "AbortError") showTopMessage("无法读取文件夹", "请重新选择音乐文件夹，并允许应用读取其中的音频。", false, "warning", false);
      }
      return;
    }
    localFolderInputRef.current?.click();
  };

  const handleLocalFolderFiles = async (event) => {
    const files = Array.from(event.target.files || []);
    const entries = files.map((file) => {
      const relativePath = file.webkitRelativePath || file.name;
      const parts = relativePath.split("/");
      return { file, path: parts.length > 2 ? `${parts.slice(1, -1).join(" / ")} / ` : "" };
    });
    const folderName = files[0]?.webkitRelativePath?.split("/")[0] || "本地音乐";
    await loadLocalTrackFiles(entries, folderName);
    event.target.value = "";
  };

  const toggleLocalPlayback = async () => {
    const audio = localAudioRef.current;
    if (!audio || !currentLocalTrack) return;
    if (audio.paused) {
      await audio.play().catch(() => showTopMessage("暂时无法播放", "请检查音乐文件是否仍然可用。", false, "warning", false));
    } else {
      audio.pause();
    }
  };

  const changeLocalTrack = (direction) => {
    if (!localTracks.length) return;
    if (localTracks.length === 1) {
      const audio = localAudioRef.current;
      if (audio) {
        audio.currentTime = 0;
        audio.play().catch(() => {});
      }
      return;
    }
    localAutoplayRef.current = true;
    setLocalTrackIndex((current) => {
      if (localPlaybackMode === "shuffle" && direction > 0) {
        let next = current;
        while (next === current) next = Math.floor(Math.random() * localTracks.length);
        return next;
      }
      return (current + direction + localTracks.length) % localTracks.length;
    });
  };

  const handleLocalTrackEnded = () => {
    const audio = localAudioRef.current;
    if (localPlaybackMode === "repeat-one" && audio) {
      audio.currentTime = 0;
      audio.play().catch(() => {});
      return;
    }
    changeLocalTrack(1);
  };

  const cycleLocalPlaybackMode = () => {
    setLocalPlaybackMode((current) => current === "sequence" ? "repeat-one" : current === "repeat-one" ? "shuffle" : "sequence");
  };

  const seekLocalTrack = (event) => {
    const audio = localAudioRef.current;
    if (!audio) return;
    const nextTime = Number(event.target.value);
    audio.currentTime = nextTime;
    setLocalCurrentTime(nextTime);
  };

  const changeLocalVolume = (event) => {
    const nextVolume = Number(event.target.value);
    setLocalVolume(nextVolume);
    if (localAudioRef.current) {
      localAudioRef.current.volume = nextVolume;
      if (nextVolume > 0) localAudioRef.current.muted = false;
    }
    if (nextVolume > 0) setLocalMuted(false);
  };

  const toggleLocalMute = () => {
    setLocalMuted((current) => {
      const next = !current;
      if (localAudioRef.current) localAudioRef.current.muted = next;
      return next;
    });
  };

  const connectSpotify = async () => {
    const clientId = spotifyClientId.trim();
    if (!clientId) {
      showTopMessage("先填写 Client ID", "在 Spotify Developer Dashboard 创建应用后，把 Client ID 粘贴到这里。", false, "warning", false);
      return;
    }
    localStorage.setItem(SPOTIFY_CLIENT_ID_STORAGE_KEY, clientId);

    if (spotifyNeedsLoopback) {
      showTopMessage("请使用 127.0.0.1 打开", "Spotify 不允许 localhost 作为本地回调地址，正在切换安全的本地地址。", false, "warning", false);
      window.setTimeout(() => window.location.assign(spotifyLoopbackUrl(clientId)), 700);
      return;
    }

    try {
      setSpotifyStatus("connecting");
      const authorizationUrl = await buildSpotifyAuthorizationUrl(clientId);
      window.location.assign(authorizationUrl);
    } catch {
      setSpotifyStatus("error");
      showTopMessage("无法开始 Spotify 授权", "请确认浏览器允许安全加密功能后重试。", false, "warning", false);
    }
  };

  const disconnectSpotify = () => {
    localStorage.removeItem(SPOTIFY_AUTH_STORAGE_KEY);
    localStorage.removeItem(SPOTIFY_VERIFIER_STORAGE_KEY);
    localStorage.removeItem(SPOTIFY_STATE_STORAGE_KEY);
    spotifyAuthRef.current = null;
    setSpotifyAuth(null);
    setSpotifyStatus("idle");
    setMediaInfo({ available: false, title: "连接 Spotify", artist: "", artwork: "" });
    showTopMessage("Spotify 已断开", "本地授权令牌已清除。", false, "info", false);
  };

  const announcePhaseStart = (nextMode, requestPermission = false, prefix = "") => {
    const isFocus = nextMode === "focus";
    const title = isFocus ? "专注开始" : "短休开始";
    const body = isFocus
      ? `接下来 ${timerSettings.focus} 分钟，把注意力留在当前任务。`
      : `休息 ${timerSettings.short} 分钟，放松眼睛和肩颈。`;
    showTopMessage(prefix ? `${prefix} · ${title}` : title, body, requestPermission, isFocus ? "focus" : "break");
  };

  useEffect(() => {
    localStorage.setItem("moke-tasks", JSON.stringify(tasks));
    setTaskLists((current) =>
      current.map((list) => (list.id === activeListId ? { ...list, tasks } : list)),
    );
    if (!tasks.some((task) => task.id === activeTaskId)) {
      setActiveTaskId(tasks[0]?.id ?? null);
    }
  }, [activeListId, activeTaskId, tasks]);

  useEffect(() => {
    try {
      if (localStorage.getItem(FACTORY_DEFAULTS_STORAGE_KEY)) return;
      const normalizedLists = taskLists.map((list) => (
        list.id === activeListId ? { ...list, tasks: normalizeTasks(tasks) } : { ...list, tasks: normalizeTasks(list.tasks) }
      ));
      const factoryDefaults = {
        version: FACTORY_DEFAULTS_VERSION,
        capturedAt: new Date().toISOString(),
        dashboard: { gridLayout: dashboardGridLayout },
        configuration: {
          taskLists: normalizedLists,
          activeListId,
          activeTaskId,
          tasks: normalizeTasks(tasks),
        },
        personalization: {
          appearanceMode,
          theme,
          paletteId,
          themeVariant,
          pureBlack,
          fontScaleEnabled,
          fontScale,
        },
        general: {
          immersive,
          railCompact,
          timerSettings,
          analogClockSettings,
          countdown,
          contextLayout,
          quoteMode,
          quickNoteLauncherVisible,
          translatorLauncherVisible,
          calculatorLauncherVisible,
          mediaSource,
          localVolume,
          localPlaybackMode,
          spotifyClientId,
        },
      };
      localStorage.setItem(FACTORY_DEFAULTS_STORAGE_KEY, JSON.stringify(factoryDefaults));
    } catch {
      // A restricted storage environment falls back to the bundled defaults.
    }
  }, []);

  useEffect(() => {
    localStorage.setItem("moke-task-lists", JSON.stringify(taskLists));
  }, [taskLists]);

  useEffect(() => {
    localStorage.setItem("moke-active-list", activeListId);
  }, [activeListId]);

  useEffect(() => {
    if (activeTaskId == null) localStorage.removeItem(ACTIVE_TASK_STORAGE_KEY);
    else localStorage.setItem(ACTIVE_TASK_STORAGE_KEY, JSON.stringify(activeTaskId));
  }, [activeTaskId]);

  useEffect(() => {
    localStorage.setItem("moke-timer-settings", JSON.stringify(timerSettings));
  }, [timerSettings]);

  useEffect(() => {
    localStorage.setItem("moke-focus-logs", JSON.stringify(focusLogs));
  }, [focusLogs]);

  useEffect(() => () => {
    window.clearTimeout(topNoticeTimerRef.current);
    window.clearTimeout(logoCooldownTimerRef.current);
  }, []);

  useEffect(() => {
    const hideTimers = new Map();
    const findScrollableSurface = (eventTarget) => {
      if (eventTarget === document) return document.scrollingElement;
      let node = eventTarget instanceof Element ? eventTarget : null;
      while (node) {
        const style = getComputedStyle(node);
        const canScrollY = /(auto|scroll)/.test(style.overflowY) && node.scrollHeight > node.clientHeight;
        const canScrollX = /(auto|scroll)/.test(style.overflowX) && node.scrollWidth > node.clientWidth;
        if (canScrollY || canScrollX) return node;
        node = node.parentElement;
      }
      return document.scrollingElement;
    };
    const revealActiveScrollbar = (event) => {
      const surface = findScrollableSurface(event.target);
      if (!(surface instanceof Element)) return;
      surface.classList.add("is-scroll-active");
      window.clearTimeout(hideTimers.get(surface));
      hideTimers.set(surface, window.setTimeout(() => {
        surface.classList.remove("is-scroll-active");
        hideTimers.delete(surface);
      }, 900));
    };
    document.addEventListener("scroll", revealActiveScrollbar, true);
    document.addEventListener("wheel", revealActiveScrollbar, { capture: true, passive: true });
    return () => {
      document.removeEventListener("scroll", revealActiveScrollbar, true);
      document.removeEventListener("wheel", revealActiveScrollbar, true);
      hideTimers.forEach((timer, target) => {
        window.clearTimeout(timer);
        target.classList.remove("is-scroll-active");
      });
    };
  }, []);

  useEffect(() => {
    localStorage.setItem("moke-stats", JSON.stringify(stats));
  }, [stats]);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem("moke-theme", theme);
    if (window.chrome?.webview?.postMessage) {
      window.chrome.webview.postMessage(`app-theme|${theme}`);
    }
  }, [theme]);

  useEffect(() => {
    localStorage.setItem("moke-theme-mode", appearanceMode);
  }, [appearanceMode]);

  useEffect(() => {
    localStorage.setItem(IMMERSIVE_STORAGE_KEY, String(immersive));
    document.documentElement.classList.toggle("is-immersive-root", immersive);
    document.body.classList.toggle("is-immersive-root", immersive);
    return () => {
      document.documentElement.classList.remove("is-immersive-root");
      document.body.classList.remove("is-immersive-root");
    };
  }, [immersive]);

  useEffect(() => {
    localStorage.setItem(RAIL_COMPACT_STORAGE_KEY, String(railCompact));
  }, [railCompact]);

  useEffect(() => {
    localStorage.setItem(LOCAL_VOLUME_STORAGE_KEY, String(localVolume));
  }, [localVolume]);

  useEffect(() => {
    localStorage.setItem(LOCAL_PLAYBACK_MODE_STORAGE_KEY, localPlaybackMode);
  }, [localPlaybackMode]);

  useEffect(() => {
    localStorage.setItem("moke-palette", paletteId);
  }, [paletteId]);

  useEffect(() => {
    localStorage.setItem("moke-theme-variant", themeVariant);
  }, [themeVariant]);

  useEffect(() => {
    localStorage.setItem("moke-pure-black", String(pureBlack));
  }, [pureBlack]);

  useEffect(() => {
    localStorage.setItem("moke-font-scale-enabled", String(fontScaleEnabled));
    localStorage.setItem("moke-font-scale", String(fontScale));
  }, [fontScale, fontScaleEnabled]);

  useEffect(() => {
    localStorage.setItem(QUOTE_MODE_STORAGE_KEY, String(quoteMode));
    if (!quoteMode) {
      setRemoteQuote("");
      return undefined;
    }
    let cancelled = false;
    const rotateQuote = async () => {
      setQuoteIndex((current) => (current + 1) % FOCUS_QUOTES.length);
      try {
        const response = await fetch("https://v1.hitokoto.cn/?c=d&c=e&c=k&encode=json&max_length=28", {
          cache: "no-store",
        });
        if (!response.ok) throw new Error("quote unavailable");
        const data = await response.json();
        const quote = typeof data?.hitokoto === "string" ? data.hitokoto.trim() : "";
        if (!cancelled && quote.length >= 6 && quote.length <= 28) setRemoteQuote(quote);
      } catch {
        if (!cancelled) setRemoteQuote("");
      }
    };
    rotateQuote();
    const quoteTimer = window.setInterval(rotateQuote, 5 * 60 * 1000);
    return () => {
      cancelled = true;
      window.clearInterval(quoteTimer);
    };
  }, [quoteMode]);

  useEffect(() => {
    localStorage.setItem(QUICK_NOTE_LAUNCHER_STORAGE_KEY, String(quickNoteLauncherVisible));
  }, [quickNoteLauncherVisible]);

  useEffect(() => {
    localStorage.setItem(TRANSLATOR_LAUNCHER_STORAGE_KEY, String(translatorLauncherVisible));
    if (!translatorLauncherVisible) setTranslatorOpen(false);
  }, [translatorLauncherVisible]);

  useEffect(() => {
    localStorage.setItem(CALCULATOR_LAUNCHER_STORAGE_KEY, String(calculatorLauncherVisible));
    if (!calculatorLauncherVisible) setCalculatorOpen(false);
  }, [calculatorLauncherVisible]);

  useEffect(() => {
    localStorage.setItem(ANALOG_CLOCK_STORAGE_KEY, JSON.stringify(analogClockSettings));
  }, [analogClockSettings]);

  useEffect(() => {
    localStorage.setItem("moke-context-layout", JSON.stringify(contextLayout));
  }, [contextLayout]);

  useEffect(() => {
    localStorage.setItem(COUNTDOWN_STORAGE_KEY, JSON.stringify(countdown));
  }, [countdown]);

  useEffect(() => {
    localStorage.setItem(QUICK_NOTES_STORAGE_KEY, JSON.stringify(quickNotes));
  }, [quickNotes]);

  useEffect(() => {
    try {
      sessionStorage.removeItem(QUICK_NOTE_GUIDE_OPEN_KEY);
    } catch {
      // Session storage can be unavailable in hardened browser modes.
    }
  }, []);

  useEffect(() => {
    quickNotePositionRef.current = quickNotePosition;
  }, [quickNotePosition]);

  useEffect(() => {
    translatorPositionRef.current = translatorPosition;
  }, [translatorPosition]);

  useEffect(() => {
    calculatorPositionRef.current = calculatorPosition;
  }, [calculatorPosition]);

  useEffect(() => {
    stopwatchPositionRef.current = stopwatchPosition;
  }, [stopwatchPosition]);

  useEffect(() => {
    const keepQuickNoteInView = () => {
      setQuickNotePosition((current) => clampQuickNotePosition(current));
      setTranslatorPosition((current) => clampTranslatorPosition(current));
      setCalculatorPosition((current) => clampCalculatorPosition(current));
      setStopwatchPosition((current) => clampStopwatchPosition(current));
    };
    const closeQuickNoteWithEscape = (event) => {
      if (event.key === "Escape") {
        setOpenQuickNoteId(null);
        setTranslatorOpen(false);
        setCalculatorOpen(false);
        setStopwatchOpen(false);
      }
    };
    window.addEventListener("resize", keepQuickNoteInView);
    window.addEventListener("keydown", closeQuickNoteWithEscape);
    return () => {
      window.removeEventListener("resize", keepQuickNoteInView);
      window.removeEventListener("keydown", closeQuickNoteWithEscape);
      window.clearTimeout(quickNoteDeleteTimerRef.current);
    };
  }, []);

  useEffect(() => {
    const maxIndex = Math.max(tasks.length - visibleTaskCount, 0);
    if (taskSlide > maxIndex) setTaskSlide(maxIndex);
  }, [taskSlide, tasks.length, visibleTaskCount]);

  useEffect(() => {
    const shell = taskSliderShellRef.current;
    if (!shell) return undefined;
    const updateVisibleCount = () => {
      const available = Math.max(1, Math.floor((shell.clientHeight - 68) / TASK_ROW_STEP));
      setVisibleTaskCount(Math.min(TASKS_VISIBLE, available));
    };
    updateVisibleCount();
    const observer = new ResizeObserver(updateVisibleCount);
    observer.observe(shell);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!settingsOpen && !dashboardVisible && !toolsOpen) return undefined;
    const closeOnEscape = (event) => {
      if (event.key === "Escape") {
        setSettingsOpen(false);
        setToolsOpen(false);
        if (!dashboardVisible) syncWorkspaceUrl("dashboard");
      }
      if (event.key === "Escape" && dashboardVisible) closeDashboard();
    };
    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [dashboardVisible, settingsOpen, toolsOpen]);

  useEffect(() => {
    document.title = isRunning ? `${minuteText}:${secondText} · Tomatotodo` : "Tomatotodo";
  }, [isRunning, minuteText, secondText]);

  useEffect(() => {
    if (!miniWindowMode) return;
    const modeLabel = countUpEnabled ? "正向计时" : MODES[mode].label;
    const runningLabel = isRunning ? "暂停" : "继续";
    const time = `${minuteText}:${secondText}`;
    const miniProgress = Math.round(Math.min(1, Math.max(0, progress)) * 100);

    if (window.chrome?.webview?.postMessage) {
      window.chrome.webview.postMessage(`mini-window|update|${modeLabel}|${runningLabel}|${time}|${miniProgress}`);
      return;
    }

    const miniWindow = miniWindowRef.current;
    if (!miniWindow || miniWindow.closed) return;
    const miniDocument = miniWindow.document;
    const shell = document.querySelector(".app-shell");
    const tokens = shell ? getComputedStyle(shell) : getComputedStyle(document.documentElement);
    miniDocument.documentElement.style.setProperty("--mini-bg", tokens.getPropertyValue("--bg"));
    miniDocument.documentElement.style.setProperty("--mini-surface", tokens.getPropertyValue("--surface-raised"));
    miniDocument.documentElement.style.setProperty("--mini-text", tokens.getPropertyValue("--text"));
    miniDocument.documentElement.style.setProperty("--mini-muted", tokens.getPropertyValue("--muted"));
    miniDocument.documentElement.style.setProperty("--mini-accent", tokens.getPropertyValue("--accent"));
    const modeNode = miniDocument.getElementById("mini-mode");
    const statusNode = miniDocument.getElementById("mini-status");
    const timeNode = miniDocument.getElementById("mini-time");
    if (modeNode) modeNode.textContent = modeLabel;
    if (statusNode) {
      statusNode.textContent = runningLabel;
      statusNode.setAttribute("aria-label", isRunning ? "暂停计时" : "继续计时");
      statusNode.setAttribute("title", isRunning ? "暂停计时" : "继续计时");
      statusNode.dataset.running = isRunning ? "true" : "false";
      statusNode.style.setProperty("--mini-progress", `${miniProgress}%`);
    }
    if (timeNode) timeNode.textContent = time;
  }, [miniWindowMode, mode, countUpEnabled, isRunning, minuteText, secondText, progress, theme, paletteId]);

  useEffect(() => () => {
    if (window.chrome?.webview?.postMessage) {
      window.chrome.webview.postMessage("mini-window|hide");
      return;
    }
    if (miniWindowRef.current && !miniWindowRef.current.closed) miniWindowRef.current.close();
  }, []);

  useEffect(() => {
    if (!isRunning) return undefined;
    const timer = window.setInterval(() => {
      setSecondsLeft((value) => countUpEnabled ? value + 1 : Math.max(0, value - 1));
    }, 1000);
    return () => window.clearInterval(timer);
  }, [isRunning, countUpEnabled]);

  useEffect(() => {
    if (countUpEnabled || !isRunning || secondsLeft !== 0 || completedRef.current) return;
    completedRef.current = true;
    playChime();

    if (mode === "focus") {
      const taskEstimate = parseTaskEstimate(activeTask?.estimatedPomodoros);
      const completedForTask = activeTask ? (completedPomodorosByTask.get(activeTask.id) || 0) + 1 : 0;
      const taskGoalReached = Boolean(taskEstimate && completedForTask >= taskEstimate);
      commitFocusSegment("completed");
      setStats((current) => ({
        ...current,
        sessions: current.sessions + 1,
        minutes: current.minutes + timerSettings.focus,
      }));
      committedSecondsRef.current = 0;
      if (taskGoalReached) {
        setTasks((current) => current.map((task) => (
          task.id === activeTask.id ? { ...task, done: true } : task
        )));
        setIsRunning(false);
        playTaskCompletionSound();
        showTopMessage(
          "任务目标完成",
          `${activeTask.title} 已完成 ${taskEstimate} 个预计番茄钟，自动计时已停止。`,
          false,
          "tomato",
        );
      } else if (timerSettings.shortBreakEnabled) {
        setMode("short");
        setSecondsLeft(timerSettings.short * 60);
        completedRef.current = false;
        setIsRunning(true);
        showTopMessage(
          "专注完成 · 获得 1 个番茄 · 短休开始",
          `这个番茄已经存入档案。现在休息 ${timerSettings.short} 分钟。`,
          false,
          "tomato",
        );
      } else if (taskEstimate) {
        setMode("focus");
        setSecondsLeft(timerSettings.focus * 60);
        completedRef.current = false;
        setIsRunning(true);
        showTopMessage(
          `获得 1 个番茄 · 自动继续 ${completedForTask}/${taskEstimate}`,
          "短休已关闭，下一轮专注现在开始。",
          false,
          "tomato",
        );
      } else {
        setIsRunning(false);
        showTopMessage(
          "专注完成 · 获得 1 个番茄",
          "这个番茄已经存入今天的专注档案。",
          false,
          "tomato",
        );
      }
    } else {
      setMode("focus");
      setSecondsLeft(timerSettings.focus * 60);
      committedSecondsRef.current = 0;
      completedRef.current = false;
      setIsRunning(true);
      announcePhaseStart("focus", false, "短休完成");
    }
  }, [activeTask, completedPomodorosByTask, countUpEnabled, isRunning, mode, secondsLeft, timerSettings.focus, timerSettings.short, timerSettings.shortBreakEnabled]);

  const chooseMode = (nextMode) => {
    if (countUpEnabled) return;
    if (nextMode === "short" && !timerSettings.shortBreakEnabled) return;
    commitFocusSegment("switched");
    setMode(nextMode);
    setSecondsLeft(timerSettings[nextMode] * 60);
    setIsRunning(false);
    completedRef.current = false;
    committedSecondsRef.current = 0;
  };

  const toggleTimer = () => {
    if (!countUpEnabled && secondsLeft === 0) {
      setSecondsLeft(duration);
      completedRef.current = false;
    }
    if (isRunning) {
      commitFocusSegment("paused");
      setIsRunning(false);
      return;
    }
    setIsRunning(true);
    if (countUpEnabled) {
      showTopMessage("正向计时开始", "碎片时间会在暂停、重置或切换任务时写入专注日志。", false, "focus");
    } else {
      announcePhaseStart(mode, true);
    }
  };
  toggleTimerRef.current = toggleTimer;

  const resetTimer = () => {
    commitFocusSegment("reset");
    setIsRunning(false);
    setSecondsLeft(countUpEnabled ? 0 : duration);
    completedRef.current = false;
    committedSecondsRef.current = 0;
  };

  useEffect(() => {
    if (!window.chrome?.webview?.addEventListener) return undefined;
    const handleNativeMiniWindow = (event) => {
      if (event.data === "mini-window-hidden") setMiniWindowMode(false);
      if (event.data === "mini-window-toggle") toggleTimerRef.current?.();
    };
    window.chrome.webview.addEventListener("message", handleNativeMiniWindow);
    return () => window.chrome.webview.removeEventListener("message", handleNativeMiniWindow);
  }, []);

  const toggleDone = (id) => {
    const task = tasks.find((item) => item.id === id);
    if (!task) return;
    setTasks((current) =>
      current.map((task) => (task.id === id ? { ...task, done: !task.done } : task)),
    );
    if (!task.done) playTaskCompletionSound();
  };

  const updateTaskTitle = (id, title) => {
    setTasks((current) =>
      current.map((task) => (task.id === id ? { ...task, title } : task)),
    );
  };

  const addPresetTask = (event) => {
    event.preventDefault();
    const title = presetTaskDraft.trim();
    if (!title) return;
    const task = { id: Date.now(), title, done: false, estimatedPomodoros: parseTaskEstimate(presetTaskEstimate) };
    setTasks((current) => [...current, task]);
    if (activeTaskId == null) setActiveTaskId(task.id);
    setPresetTaskDraft("");
    setPresetTaskEstimate("");
  };

  const selectTask = (id) => {
    if (suppressTaskClickRef.current) return;
    if (id === activeTaskId) return;
    commitFocusSegment("task-switched");
    setActiveTaskId(id);
    committedSecondsRef.current = elapsedSeconds;
  };

  const reorderTasks = (sourceId, targetId) => {
    if (sourceId === targetId) return;
    setTasks((current) => {
      const sourceIndex = current.findIndex((task) => task.id === sourceId);
      const targetIndex = current.findIndex((task) => task.id === targetId);
      if (sourceIndex < 0 || targetIndex < 0) return current;
      const next = [...current];
      const [moved] = next.splice(sourceIndex, 1);
      next.splice(targetIndex, 0, moved);
      return next;
    });
  };

  const reorderDashboardWidget = (sourceId, targetId) => {
    if (!sourceId || sourceId === targetId) return;
    setDashboardGridLayout((current) => {
      const order = [...current.order];
      const sourceIndex = order.indexOf(sourceId);
      const targetIndex = order.indexOf(targetId);
      if (sourceIndex < 0 || targetIndex < 0) return current;
      const [moved] = order.splice(sourceIndex, 1);
      order.splice(targetIndex, 0, moved);
      return { ...current, order };
    });
  };

  const startDashboardWidgetLongPress = (event, widgetId) => {
    if (!dashboardEditing) return;
    if (event.pointerType === "mouse" && event.button !== 0) return;
    if (event.target.closest("button, input, textarea, select, a, [role='button']")) return;
    window.clearTimeout(dashboardWidgetDragRef.current?.timer);
    const card = event.currentTarget;
    const pending = {
      widgetId,
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      card,
      dragging: false,
      timer: null,
    };
    pending.timer = window.setTimeout(() => {
      pending.dragging = true;
      setDraggingDashboardWidget(widgetId);
      window.getSelection()?.removeAllRanges();
      card.setPointerCapture?.(event.pointerId);
      window.navigator.vibrate?.(16);
    }, 420);
    dashboardWidgetDragRef.current = pending;
  };

  const moveDashboardWidgetLongPress = (event) => {
    const pending = dashboardWidgetDragRef.current;
    if (!pending || pending.pointerId !== event.pointerId) return;
    if (!pending.dragging) {
      if (Math.hypot(event.clientX - pending.startX, event.clientY - pending.startY) > 10) {
        window.clearTimeout(pending.timer);
        dashboardWidgetDragRef.current = null;
      }
      return;
    }
    event.preventDefault();
    const target = document.elementFromPoint(event.clientX, event.clientY)?.closest?.("[data-dashboard-widget]");
    const targetId = target?.dataset?.dashboardWidget;
    if (targetId && targetId !== pending.widgetId) reorderDashboardWidget(pending.widgetId, targetId);
  };

  const finishDashboardWidgetLongPress = (event) => {
    const pending = dashboardWidgetDragRef.current;
    if (!pending || pending.pointerId !== event.pointerId) return;
    window.clearTimeout(pending.timer);
    pending.card?.releasePointerCapture?.(event.pointerId);
    dashboardWidgetDragRef.current = null;
    setDraggingDashboardWidget(null);
  };

  const setDashboardWidgetVisibility = (widgetId, visible) => {
    setDashboardGridLayout((current) => ({
      ...current,
      visible: { ...current.visible, [widgetId]: visible },
    }));
  };

  const saveDashboardGrid = () => {
    localStorage.setItem(DASHBOARD_GRID_STORAGE_KEY, JSON.stringify(dashboardGridLayout));
    setDashboardEditing(false);
    setDashboardAddOpen(false);
    setDraggingDashboardWidget(null);
    showTopMessage("仪表盘布局已保存", "组件位置与显示状态已保存在当前设备。", false, "info", false);
  };

  const startTaskLongPress = (event, taskId) => {
    if (event.pointerType === "mouse" && event.button !== 0) return;
    if (event.target.closest(".task-check, .task-delete, .preset-task-check, .preset-task-delete")) return;
    window.clearTimeout(taskLongPressRef.current?.timer);
    const row = event.currentTarget;
    const pending = {
      taskId,
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      row,
      timer: null,
    };
    pending.timer = window.setTimeout(() => {
      taskReorderRef.current = { taskId, pointerId: event.pointerId };
      suppressTaskClickRef.current = true;
      setReorderingTaskId(taskId);
      if (row.contains(document.activeElement) && document.activeElement?.matches?.("input")) {
        document.activeElement.blur();
      }
      window.getSelection()?.removeAllRanges();
      row.setPointerCapture?.(event.pointerId);
      window.navigator.vibrate?.(18);
    }, 520);
    taskLongPressRef.current = pending;
  };

  const moveTaskLongPress = (event) => {
    const pending = taskLongPressRef.current;
    const drag = taskReorderRef.current;
    if (!drag && pending?.pointerId === event.pointerId) {
      if (Math.hypot(event.clientX - pending.startX, event.clientY - pending.startY) > 10) {
        window.clearTimeout(pending.timer);
        taskLongPressRef.current = null;
      }
      return;
    }
    if (!drag || drag.pointerId !== event.pointerId) return;
    event.preventDefault();
    const target = document.elementFromPoint(event.clientX, event.clientY)?.closest?.("[data-task-reorder-id]");
    if (!target) return;
    const targetId = Number(target.dataset.taskReorderId);
    if (Number.isFinite(targetId)) reorderTasks(drag.taskId, targetId);
  };

  const finishTaskLongPress = (event) => {
    const pending = taskLongPressRef.current;
    if (pending?.pointerId === event.pointerId) window.clearTimeout(pending.timer);
    taskLongPressRef.current = null;
    if (taskReorderRef.current?.pointerId !== event.pointerId) return;
    taskReorderRef.current = null;
    setReorderingTaskId(null);
    window.setTimeout(() => {
      suppressTaskClickRef.current = false;
    }, 0);
  };

  const removeTask = (id) => {
    if (id === activeTaskId) commitFocusSegment("task-removed");
    setTasks((current) => {
      const next = current.filter((task) => task.id !== id);
      if (activeTaskId === id) setActiveTaskId(next[0]?.id ?? null);
      return next;
    });
  };

  const addTask = (event) => {
    event.preventDefault();
    const title = newTask.trim();
    if (!title) return;
    commitFocusSegment("task-switched");
    const task = { id: Date.now(), title, done: false, estimatedPomodoros: parseTaskEstimate(newTaskEstimate) };
    setTasks((current) => [...current, task]);
    setActiveTaskId(task.id);
    committedSecondsRef.current = elapsedSeconds;
    setTaskSlide(Math.max(tasks.length + 1 - visibleTaskCount, 0));
    setNewTask("");
    setNewTaskEstimate("");
    setIsAdding(false);
  };

  const slideTasks = (direction) => {
    const next = Math.min(Math.max(taskSlide + direction, 0), maxTaskSlide);
    setTaskSlide(next);
    taskListRef.current?.scrollTo({ top: next * TASK_ROW_STEP, behavior: "smooth" });
  };

  const switchTaskList = (list) => {
    commitFocusSegment("list-switched");
    setActiveListId(list.id);
    setTasks(list.tasks);
    setActiveTaskId(list.tasks[0]?.id ?? null);
    setTaskSlide(0);
    committedSecondsRef.current = elapsedSeconds;
  };

  const createTaskList = () => {
    const name = listDraftName.trim() || `新清单 ${taskLists.length + 1}`;
    const list = { id: `list-${Date.now()}`, name, tasks: [] };
    setTaskLists((current) => [...current, list]);
    setListDraftName("");
    switchTaskList(list);
  };

  const renameActiveList = () => {
    const name = listDraftName.trim();
    if (!name) return;
    setTaskLists((current) =>
      current.map((list) => (list.id === activeListId ? { ...list, name } : list)),
    );
    setListDraftName("");
  };

  const deleteActiveList = () => {
    if (taskLists.length <= 1) {
      showTopMessage("至少保留一份任务清单", "请先新建另一份清单后再删除。", false, "warning", false);
      return;
    }
    const nextLists = taskLists.filter((list) => list.id !== activeListId);
    const nextList = nextLists[0];
    setTaskLists(nextLists);
    setActiveListId(nextList.id);
    setTasks(nextList.tasks);
    setActiveTaskId(nextList.tasks[0]?.id ?? null);
    setTaskSlide(0);
    setListDraftName("");
  };

  const openCreatePreset = () => {
    setEditingPresetId(null);
    setPresetBuilder({ name: "", tasks: [] });
    setPresetBuilderTask({ title: "", subtitle: "", estimatedPomodoros: "" });
    setPresetCardMenuId(null);
    setPresetDrawerOpen(true);
  };

  const openEditPreset = (list) => {
    if (!list) return;
    setEditingPresetId(list.id);
    setPresetBuilder({
      name: list.name || "",
      tasks: normalizeTasks(list.tasks).map((task) => ({ ...task, subtitle: task.subtitle || "" })),
    });
    setPresetBuilderTask({ title: "", subtitle: "", estimatedPomodoros: "" });
    setPresetCardMenuId(null);
    setPresetDrawerOpen(true);
  };

  const addPresetBuilderTask = (event) => {
    event.preventDefault();
    const title = presetBuilderTask.title.trim();
    if (!title) return;
    setPresetBuilder((current) => ({
      ...current,
      tasks: [...current.tasks, {
        id: Date.now(),
        title,
        subtitle: presetBuilderTask.subtitle.trim(),
        done: false,
        estimatedPomodoros: parseTaskEstimate(presetBuilderTask.estimatedPomodoros),
      }],
    }));
    setPresetBuilderTask({ title: "", subtitle: "", estimatedPomodoros: "" });
  };

  const updatePresetBuilderTask = (taskId, patch) => {
    setPresetBuilder((current) => ({
      ...current,
      tasks: current.tasks.map((task) => task.id === taskId ? { ...task, ...patch } : task),
    }));
  };

  const reorderPresetBuilderTasks = (sourceId, targetId) => {
    if (sourceId == null || targetId == null || sourceId === targetId) return;
    setPresetBuilder((current) => {
      const source = current.tasks.find((task) => task.id === sourceId);
      if (!source) return current;
      const tasks = current.tasks.filter((task) => task.id !== sourceId);
      const targetIndex = tasks.findIndex((task) => task.id === targetId);
      tasks.splice(targetIndex < 0 ? tasks.length : targetIndex, 0, source);
      return { ...current, tasks };
    });
  };

  const savePresetBuilder = () => {
    const name = presetBuilder.name.trim();
    if (!name) {
      showTopMessage("请填写清单名称", "清单名称不能为空。", false, "warning", false);
      return;
    }
    const nextTasks = presetBuilder.tasks.map((task) => ({
      ...task,
      title: task.title.trim() || "未命名任务",
      subtitle: task.subtitle?.trim() || "",
      estimatedPomodoros: parseTaskEstimate(task.estimatedPomodoros),
    }));

    if (editingPresetId) {
      setTaskLists((current) => current.map((list) => list.id === editingPresetId ? { ...list, name, tasks: nextTasks } : list));
      if (editingPresetId === activeListId) setTasks(nextTasks);
    } else {
      const nextList = { id: `list-${Date.now()}`, name, tasks: nextTasks };
      commitFocusSegment("list-switched");
      setTaskLists((current) => [...current, nextList]);
      setActiveListId(nextList.id);
      setTasks(nextTasks);
      setActiveTaskId(nextTasks[0]?.id ?? null);
      setTaskSlide(0);
      committedSecondsRef.current = 0;
    }

    setPresetDrawerOpen(false);
    setEditingPresetId(null);
    showTopMessage(
      editingPresetId ? "任务清单已更新" : "任务清单已添加",
      editingPresetId ? "清单内容与番茄预算已保存。" : "新清单已添加并设为当前清单。",
      false,
      "info",
      false,
    );
  };

  const deleteTaskListById = (listId) => {
    if (taskLists.length <= 1) {
      showTopMessage("至少保留一份任务清单", "请先新建另一份清单后再删除。", false, "warning", false);
      setPresetCardMenuId(null);
      return;
    }
    const nextLists = taskLists.filter((list) => list.id !== listId);
    setTaskLists(nextLists);
    if (listId === activeListId) {
      const nextList = nextLists[0];
      commitFocusSegment("list-switched");
      setActiveListId(nextList.id);
      setTasks(nextList.tasks);
      setActiveTaskId(nextList.tasks[0]?.id ?? null);
      setTaskSlide(0);
      committedSecondsRef.current = 0;
    }
    setPresetCardMenuId(null);
  };

  const openPresetSort = () => {
    setPresetCardMenuId(null);
    setPresetDrawerOpen(false);
    setPresetSortDraft(taskLists.map((list) => list.id));
    setDraggedPresetId(null);
    setPresetSortOpen(true);
  };

  const reorderPresetSortItems = (sourceId, targetId) => {
    if (!sourceId || !targetId || sourceId === targetId) return;
    setPresetSortDraft((current) => {
      const next = current.filter((id) => id !== sourceId);
      const targetIndex = next.indexOf(targetId);
      next.splice(targetIndex < 0 ? next.length : targetIndex, 0, sourceId);
      return next;
    });
  };

  const savePresetSort = () => {
    setTaskLists((current) => {
      const byId = new Map(current.map((list) => [list.id, list]));
      return presetSortDraft.map((id) => byId.get(id)).filter(Boolean);
    });
    setPresetSortOpen(false);
    setDraggedPresetId(null);
    showTopMessage("清单顺序已保存", "新的清单排列顺序已保存在当前设备。", false, "info", false);
  };

  const createUserDataBackup = () => ({
    format: USER_DATA_BACKUP_FORMAT,
    version: USER_DATA_BACKUP_VERSION,
    appVersion: "1.2.0",
    exportedAt: new Date().toISOString(),
    data: {
      dashboard: {
        gridLayout: dashboardGridLayout,
      },
      configuration: {
        taskLists,
        activeListId,
        activeTaskId,
        tasks,
      },
      personalization: {
        appearanceMode,
        theme,
        paletteId,
        themeVariant,
        pureBlack,
        fontScaleEnabled,
        fontScale,
      },
      general: {
        immersive,
        timerSettings,
        analogClockSettings,
        countdown,
        contextLayout,
        quoteMode,
        quickNoteLauncherVisible,
        translatorLauncherVisible,
        calculatorLauncherVisible,
        mediaSource,
        localVolume,
        localPlaybackMode,
        spotifyClientId,
      },
      archive: {
        focusLogs,
        stats,
      },
      notes: quickNotes,
    },
    exclusions: [
      "Spotify 登录令牌",
      "本地音乐文件与文件夹授权",
      "当前计时进度与临时窗口状态",
    ],
  });

  const downloadUserDataBackup = () => {
    const backup = createUserDataBackup();
    const blob = new Blob([JSON.stringify(backup, null, 2)], { type: "application/json;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `Tomatotodo-backup-${localDateInputValue(new Date())}.json`;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    window.setTimeout(() => URL.revokeObjectURL(url), 0);
    showTopMessage("用户数据已导出", "JSON 备份已保存到下载文件夹。", false, "info", false);
  };

  const exportArchiveAsImage = async () => {
    if (archiveExporting) return;
    const appShell = document.querySelector(".app-shell");
    if (!appShell) {
      showTopMessage("无法导出档案", "当前档案页面尚未准备完成，请稍后重试。", false, "warning", false);
      return;
    }

    const fileName = `Tomatotodo-focus-archive-${localDateInputValue(new Date())}.png`;
    const saveHandle = await requestPngSaveHandle(fileName);
    if (saveHandle === false) return;

    setArchiveExporting(true);
    showTopMessage("正在生成专注明信片", "正在整理今天的专注数据。", false, "info", false);

    try {
      const logoResponse = await fetch("/icons/icon-192.png");
      if (!logoResponse.ok) throw new Error("软件 Logo 读取失败。");
      const logoDataUrl = await blobToDataUrl(await logoResponse.blob());
      const logoImage = new Image();
      logoImage.src = logoDataUrl;
      await logoImage.decode();
      if (document.fonts?.ready) await document.fonts.ready;

      const width = 1800;
      const height = 1125;
      const canvas = document.createElement("canvas");
      canvas.width = width;
      canvas.height = height;
      const context = canvas.getContext("2d");
      if (!context) throw new Error("当前环境无法创建图片画布。");

      const shellStyle = window.getComputedStyle(appShell);
      const token = (name, fallback) => shellStyle.getPropertyValue(name).trim() || fallback;
      const colors = {
        background: token("--bg", "#09120d"),
        surface: token("--surface-raised", "#18231c"),
        surfaceSoft: token("--surface-soft", "#202b23"),
        text: token("--text", "#edf5ed"),
        softText: token("--soft-text", "#c2cbc2"),
        muted: token("--muted", "#89958b"),
        line: token("--line", "#3c4b40"),
        accent: token("--accent", "#91c99a"),
      };
      const fontFamily = '"Microsoft YaHei", "PingFang SC", "Noto Sans SC", system-ui, sans-serif';
      const now = new Date();
      const dateLabel = new Intl.DateTimeFormat("zh-CN", {
        year: "numeric",
        month: "long",
        day: "numeric",
        weekday: "long",
      }).format(now);
      const formatPostcardDuration = (seconds) => {
        const safeSeconds = Math.max(0, Math.floor(Number(seconds) || 0));
        if (safeSeconds < 60) return `${safeSeconds} 秒`;
        const totalMinutes = Math.floor(safeSeconds / 60);
        if (totalMinutes < 60) return `${totalMinutes} 分钟`;
        const hours = Math.floor(totalMinutes / 60);
        const minutes = totalMinutes % 60;
        return minutes ? `${hours} 小时 ${minutes} 分` : `${hours} 小时`;
      };
      const roundedPath = (x, y, boxWidth, boxHeight, radius) => {
        const safeRadius = Math.min(radius, boxWidth / 2, boxHeight / 2);
        context.beginPath();
        context.moveTo(x + safeRadius, y);
        context.arcTo(x + boxWidth, y, x + boxWidth, y + boxHeight, safeRadius);
        context.arcTo(x + boxWidth, y + boxHeight, x, y + boxHeight, safeRadius);
        context.arcTo(x, y + boxHeight, x, y, safeRadius);
        context.arcTo(x, y, x + boxWidth, y, safeRadius);
        context.closePath();
      };
      const fitText = (value, maxWidth) => {
        const textValue = String(value || "");
        if (context.measureText(textValue).width <= maxWidth) return textValue;
        let clipped = textValue;
        while (clipped.length && context.measureText(`${clipped}…`).width > maxWidth) clipped = clipped.slice(0, -1);
        return `${clipped}…`;
      };

      context.fillStyle = colors.background;
      context.fillRect(0, 0, width, height);
      if (appShell.dataset.theme !== "light") {
        const glow = context.createRadialGradient(160, 90, 0, 160, 90, 720);
        glow.addColorStop(0, colors.surfaceSoft);
        glow.addColorStop(1, "rgba(0,0,0,0)");
        context.globalAlpha = 0.72;
        context.fillStyle = glow;
        context.fillRect(0, 0, width, height);
        context.globalAlpha = 1;
      }

      context.save();
      context.globalAlpha = 0.035;
      context.fillStyle = colors.accent;
      context.font = `700 132px ${fontFamily}`;
      context.textAlign = "right";
      context.fillText("TOMATOTODO", width - 82, 845);
      context.restore();

      const padding = 88;
      roundedPath(padding, 72, 92, 92, 24);
      context.save();
      context.clip();
      context.drawImage(logoImage, padding, 72, 92, 92);
      context.restore();

      context.textAlign = "left";
      context.fillStyle = colors.text;
      context.font = `700 34px ${fontFamily}`;
      context.fillText("Tomatotodo", 204, 112);
      context.fillStyle = colors.muted;
      context.font = `500 20px ${fontFamily}`;
      context.fillText("专注明信片", 204, 148);
      context.textAlign = "right";
      context.fillStyle = colors.softText;
      context.font = `500 24px ${fontFamily}`;
      context.fillText(dateLabel, width - padding, 118);

      context.textAlign = "left";
      context.fillStyle = colors.text;
      context.font = `700 58px ${fontFamily}`;
      context.fillText("今天的专注，清晰可见。", padding, 242);
      context.fillStyle = colors.accent;
      context.fillRect(padding, 270, 112, 6);

      const statsY = 316;
      const statsHeight = 208;
      const statsWidth = width - padding * 2;
      roundedPath(padding, statsY, statsWidth, statsHeight, 34);
      context.fillStyle = colors.surface;
      context.fill();
      context.strokeStyle = colors.line;
      context.lineWidth = 2;
      context.stroke();
      const stats = [
        ["今日专注", formatPostcardDuration(trueTodaySeconds)],
        ["完成番茄数", completedPomodoros],
        ["今日任务完成情况", `${completedTasks} / ${tasks.length}`],
        ["专注次数", `${todayFocusSessions} 次`],
      ];
      const statWidth = statsWidth / stats.length;
      stats.forEach(([label, value], index) => {
        const statX = padding + statWidth * index;
        if (index) {
          context.strokeStyle = colors.line;
          context.lineWidth = 1;
          context.beginPath();
          context.moveTo(statX, statsY + 42);
          context.lineTo(statX, statsY + statsHeight - 42);
          context.stroke();
        }
        context.fillStyle = colors.muted;
        context.font = `500 22px ${fontFamily}`;
        context.fillText(label, statX + 38, statsY + 62);
        if (index === 1) {
          if (!value) {
            context.fillStyle = colors.text;
            context.font = `600 43px ${fontFamily}`;
            context.fillText("无番茄", statX + 38, statsY + 133);
            return;
          }
          const availableWidth = statWidth - 76;
          const availableHeight = 90;
          const tomatoCount = Number(value);
          const columns = Math.min(tomatoCount, Math.ceil(Math.sqrt(tomatoCount * (availableWidth / availableHeight))));
          const rows = Math.ceil(tomatoCount / columns);
          const emojiSize = Math.max(12, Math.floor(Math.min(44, (availableWidth / columns) * 0.88, (availableHeight / rows) * 0.82)));
          const cellWidth = availableWidth / columns;
          const cellHeight = availableHeight / rows;
          context.font = `${emojiSize}px "Segoe UI Emoji", "Apple Color Emoji", sans-serif`;
          context.textAlign = "center";
          for (let tomatoIndex = 0; tomatoIndex < tomatoCount; tomatoIndex += 1) {
            const column = tomatoIndex % columns;
            const row = Math.floor(tomatoIndex / columns);
            context.fillText(
              "🍅",
              statX + 38 + column * cellWidth + cellWidth / 2,
              statsY + 88 + row * cellHeight + Math.min(cellHeight * 0.78, emojiSize),
            );
          }
          context.textAlign = "left";
          return;
        }
        context.fillStyle = colors.text;
        context.font = `600 43px ${fontFamily}`;
        context.fillText(fitText(value, statWidth - 76), statX + 38, statsY + 133);
      });

      const liveLog = liveFocusSeconds > 0 ? {
        id: "live-export",
        time: now.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit", hour12: false }),
        taskTitle: activeTask?.title || "自由专注",
        seconds: liveFocusSeconds,
      } : null;
      const postcardLogs = [...todayLogs, ...(liveLog ? [liveLog] : [])]
        .sort((left, right) => String(left.time || "").localeCompare(String(right.time || "")));
      const visibleLogs = postcardLogs.slice(0, 12);
      const logsY = 598;
      context.fillStyle = colors.text;
      context.font = `700 30px ${fontFamily}`;
      context.fillText("今日专注日志", padding, logsY);
      context.fillStyle = colors.muted;
      context.font = `500 20px ${fontFamily}`;
      context.fillText(`${postcardLogs.length} 条记录 · 累计 ${formatPostcardDuration(trueTodaySeconds)}`, padding + 222, logsY);

      if (!visibleLogs.length) {
        roundedPath(padding, logsY + 38, statsWidth, 304, 28);
        context.fillStyle = colors.surface;
        context.fill();
        context.strokeStyle = colors.line;
        context.stroke();
        context.fillStyle = colors.muted;
        context.font = `500 25px ${fontFamily}`;
        context.textAlign = "center";
        context.fillText("今天还没有专注日志。开始一段专注后，记录会出现在这里。", width / 2, logsY + 202);
      } else {
        const columnGap = 34;
        const columnWidth = (statsWidth - columnGap) / 2;
        const rowHeight = 62;
        visibleLogs.forEach((log, index) => {
          const column = index >= 6 ? 1 : 0;
          const row = index % 6;
          const x = padding + column * (columnWidth + columnGap);
          const y = logsY + 36 + row * rowHeight;
          roundedPath(x, y, columnWidth, 50, 14);
          context.fillStyle = colors.surface;
          context.fill();
          context.strokeStyle = colors.line;
          context.lineWidth = 1;
          context.stroke();
          context.textAlign = "left";
          context.fillStyle = colors.accent;
          context.font = `600 19px ${fontFamily}`;
          context.fillText(log.time || "--:--", x + 18, y + 32);
          context.fillStyle = colors.text;
          context.font = `500 20px ${fontFamily}`;
          context.fillText(fitText(log.taskTitle || "自由专注", columnWidth - 248), x + 112, y + 32);
          context.textAlign = "right";
          context.fillStyle = colors.softText;
          context.font = `500 18px ${fontFamily}`;
          context.fillText(formatPostcardDuration(log.seconds), x + columnWidth - 18, y + 32);
        });
      }

      context.textAlign = "left";
      context.fillStyle = colors.muted;
      context.font = `500 18px ${fontFamily}`;
      if (postcardLogs.length > visibleLogs.length) {
        context.fillText(`另有 ${postcardLogs.length - visibleLogs.length} 条日志未展开`, padding, 1043);
      }
      context.textAlign = "right";
      context.globalAlpha = 0.72;
      context.fillText("Tomatotodo · 让每一段专注，都有迹可循", width - padding, 1043);
      context.globalAlpha = 1;

      const pngBlob = await new Promise((resolve) => canvas.toBlob(resolve, "image/png", 1));
      if (!pngBlob || pngBlob.size < 1024) throw new Error("PNG 图片生成结果为空。");

      const saveMethod = await savePngBlob(pngBlob, fileName, saveHandle);
      showTopMessage(
        "专注明信片已导出",
        saveMethod === "picker" ? "今天的专注明信片已保存为 PNG 图片。" : "专注明信片已发送到浏览器下载列表。",
        false,
        "info",
        false,
      );
    } catch (error) {
      console.error("Archive image export failed", error);
      showTopMessage("导出失败", error instanceof Error ? error.message : "图片生成或保存失败，请稍后重试。", false, "warning", false);
    } finally {
      setArchiveExporting(false);
    }
  };

  const validateUserDataBackup = (backup) => {
    if (!backup || backup.format !== USER_DATA_BACKUP_FORMAT || backup.version !== USER_DATA_BACKUP_VERSION) {
      throw new Error("不是受支持的 Tomatotodo 备份文件。");
    }
    const data = backup.data;
    if (!data || typeof data !== "object") throw new Error("备份文件缺少用户数据。");
    if (!Array.isArray(data.configuration?.taskLists) || !Array.isArray(data.configuration?.tasks)) {
      throw new Error("任务与清单数据不完整。");
    }
    if (!data.dashboard?.gridLayout || typeof data.dashboard.gridLayout !== "object") {
      throw new Error("仪表盘布局数据不完整。");
    }
    if (!data.personalization || !data.general || !Array.isArray(data.archive?.focusLogs) || !Array.isArray(data.notes)) {
      throw new Error("设置、档案或便笺数据不完整。");
    }
    return backup;
  };

  const handleUserDataImportFile = async (event) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    setBackupImportError("");
    if (!file) return;
    if (file.size > 10 * 1024 * 1024) {
      setPendingImportBackup(null);
      setBackupImportError("文件超过 10 MB，无法导入。");
      return;
    }
    try {
      const backup = validateUserDataBackup(JSON.parse(await file.text()));
      setPendingImportBackup(backup);
    } catch (error) {
      setPendingImportBackup(null);
      setBackupImportError(error instanceof Error ? error.message : "备份文件读取失败。");
    }
  };

  const applyUserDataBackup = () => {
    if (!pendingImportBackup) return;
    const { data } = pendingImportBackup;
    const writeJson = (key, value) => localStorage.setItem(key, JSON.stringify(value));
    const writeText = (key, value) => localStorage.setItem(key, String(value));

    writeJson(DASHBOARD_GRID_STORAGE_KEY, data.dashboard.gridLayout);
    writeJson("moke-task-lists", data.configuration.taskLists);
    writeText("moke-active-list", data.configuration.activeListId || data.configuration.taskLists[0]?.id || "today");
    if (data.configuration.activeTaskId != null) writeJson(ACTIVE_TASK_STORAGE_KEY, data.configuration.activeTaskId);
    else localStorage.removeItem(ACTIVE_TASK_STORAGE_KEY);
    writeJson("moke-tasks", data.configuration.tasks);

    const personalization = data.personalization;
    writeText("moke-theme-mode", ["auto", "light", "dark"].includes(personalization.appearanceMode) ? personalization.appearanceMode : "dark");
    writeText("moke-theme", personalization.theme === "light" ? "light" : "dark");
    writeText("moke-palette", typeof personalization.paletteId === "string" ? personalization.paletteId.slice(0, 32) : "forest");
    writeText("moke-theme-variant", MATERIAL_SCHEMES.some((item) => item.id === personalization.themeVariant) ? personalization.themeVariant : "tonalSpot");
    writeText("moke-pure-black", Boolean(personalization.pureBlack));
    writeText("moke-font-scale-enabled", personalization.fontScaleEnabled !== false);
    writeText("moke-font-scale", Math.min(125, Math.max(85, Number(personalization.fontScale) || 100)));

    const general = data.general;
    writeText(IMMERSIVE_STORAGE_KEY, Boolean(general.immersive));
    writeJson("moke-timer-settings", general.timerSettings || readTimerSettings());
    writeJson(ANALOG_CLOCK_STORAGE_KEY, general.analogClockSettings || readAnalogClockSettings());
    writeJson(COUNTDOWN_STORAGE_KEY, general.countdown || defaultCountdown());
    writeJson("moke-context-layout", general.contextLayout || readContextLayout());
    writeText(QUOTE_MODE_STORAGE_KEY, Boolean(general.quoteMode));
    writeText(QUICK_NOTE_LAUNCHER_STORAGE_KEY, general.quickNoteLauncherVisible !== false);
    writeText(TRANSLATOR_LAUNCHER_STORAGE_KEY, Boolean(general.translatorLauncherVisible));
    writeText(CALCULATOR_LAUNCHER_STORAGE_KEY, Boolean(general.calculatorLauncherVisible));
    writeText(MEDIA_SOURCE_STORAGE_KEY, general.mediaSource === "local" ? "local" : "spotify");
    writeText(LOCAL_VOLUME_STORAGE_KEY, Math.min(1, Math.max(0, Number(general.localVolume) || 0.72)));
    writeText(LOCAL_PLAYBACK_MODE_STORAGE_KEY, ["sequence", "repeat-one", "shuffle"].includes(general.localPlaybackMode) ? general.localPlaybackMode : "sequence");
    if (typeof general.spotifyClientId === "string") writeText(SPOTIFY_CLIENT_ID_STORAGE_KEY, general.spotifyClientId.slice(0, 160));

    writeJson("moke-focus-logs", data.archive.focusLogs);
    writeJson("moke-stats", data.archive.stats || { date: new Date().toLocaleDateString("zh-CN"), sessions: 0, minutes: 0 });
    writeJson(QUICK_NOTES_STORAGE_KEY, data.notes);
    writeText(QUICK_NOTE_GUIDE_VERSION_KEY, "7");

    setPendingImportBackup(null);
    setBackupImportError("");
    showTopMessage("用户数据已导入", "页面将重新载入并应用备份内容。", false, "info", false);
    window.setTimeout(() => window.location.replace(`${window.location.pathname}?view=general`), 650);
  };

  const advanceFactoryReset = async () => {
    if (clearConfirmStep === 0) {
      setClearConfirmStep(1);
      showTopMessage("第一次确认", "当前任务与设置将由 1.2 默认模板覆盖，专注档案和用户便笺会被清除。", false, "warning", false);
      return;
    }
    if (clearConfirmStep === 1) {
      setClearConfirmStep(2);
      showTopMessage("最后一次确认", "此操作无法撤销。再次点击红色按钮才会恢复出厂数据。", false, "warning", false);
      return;
    }
    closeMiniWindow();
    let factoryDefaults = null;
    try {
      const savedDefaults = JSON.parse(localStorage.getItem(FACTORY_DEFAULTS_STORAGE_KEY));
      if (savedDefaults?.version === FACTORY_DEFAULTS_VERSION) factoryDefaults = savedDefaults;
    } catch {
      factoryDefaults = null;
    }
    localStorage.clear();
    sessionStorage.clear();
    if (factoryDefaults) {
      const writeJson = (key, value) => localStorage.setItem(key, JSON.stringify(value));
      const writeText = (key, value) => localStorage.setItem(key, String(value));
      const configuration = factoryDefaults.configuration;
      const personalization = factoryDefaults.personalization;
      const general = factoryDefaults.general;

      writeJson(FACTORY_DEFAULTS_STORAGE_KEY, factoryDefaults);
      writeJson(DASHBOARD_GRID_STORAGE_KEY, factoryDefaults.dashboard.gridLayout);
      writeJson("moke-task-lists", configuration.taskLists);
      writeText("moke-active-list", configuration.activeListId || configuration.taskLists[0]?.id || "today");
      writeJson("moke-tasks", configuration.tasks || configuration.taskLists[0]?.tasks || []);
      if (configuration.activeTaskId != null) writeJson(ACTIVE_TASK_STORAGE_KEY, configuration.activeTaskId);

      writeText("moke-theme-mode", personalization.appearanceMode);
      writeText("moke-theme", personalization.theme);
      writeText("moke-palette", personalization.paletteId);
      writeText("moke-theme-variant", personalization.themeVariant);
      writeText("moke-pure-black", Boolean(personalization.pureBlack));
      writeText("moke-font-scale-enabled", personalization.fontScaleEnabled !== false);
      writeText("moke-font-scale", personalization.fontScale);

      writeText(IMMERSIVE_STORAGE_KEY, Boolean(general.immersive));
      writeText(RAIL_COMPACT_STORAGE_KEY, Boolean(general.railCompact));
      writeJson("moke-timer-settings", general.timerSettings);
      writeJson(ANALOG_CLOCK_STORAGE_KEY, general.analogClockSettings);
      writeJson(COUNTDOWN_STORAGE_KEY, general.countdown);
      writeJson("moke-context-layout", general.contextLayout);
      writeText(QUOTE_MODE_STORAGE_KEY, Boolean(general.quoteMode));
      writeText(QUICK_NOTE_LAUNCHER_STORAGE_KEY, general.quickNoteLauncherVisible !== false);
      writeText(TRANSLATOR_LAUNCHER_STORAGE_KEY, Boolean(general.translatorLauncherVisible));
      writeText(CALCULATOR_LAUNCHER_STORAGE_KEY, Boolean(general.calculatorLauncherVisible));
      writeText(MEDIA_SOURCE_STORAGE_KEY, general.mediaSource === "local" ? "local" : "spotify");
      writeText(LOCAL_VOLUME_STORAGE_KEY, general.localVolume);
      writeText(LOCAL_PLAYBACK_MODE_STORAGE_KEY, general.localPlaybackMode);
      if (general.spotifyClientId) writeText(SPOTIFY_CLIENT_ID_STORAGE_KEY, general.spotifyClientId);
    }
    const cleanupJobs = [];
    if (window.indexedDB) {
      cleanupJobs.push(new Promise((resolve) => {
        const request = window.indexedDB.deleteDatabase(LOCAL_MEDIA_DB_NAME);
        request.onsuccess = resolve;
        request.onerror = resolve;
        request.onblocked = resolve;
      }));
    }
    if ("caches" in window) {
      cleanupJobs.push(window.caches.keys().then((keys) => Promise.all(keys.map((key) => window.caches.delete(key)))));
    }
    await Promise.allSettled(cleanupJobs);
    window.location.replace(`${window.location.pathname}?view=dashboard`);
  };

  const updateTimerSetting = (key, value) => {
    const nextValue = Math.min(180, Math.max(1, Number(value) || 1));
    setTimerSettings((current) => ({ ...current, [key]: nextValue }));
    if (!countUpEnabled && !isRunning && mode === key) {
      setSecondsLeft(nextValue * 60);
      committedSecondsRef.current = 0;
    }
  };

  const toggleCountUpMode = (enabled) => {
    commitFocusSegment("timer-mode-switched");
    setIsRunning(false);
    setMode("focus");
    setSecondsLeft(enabled ? 0 : timerSettings.focus * 60);
    committedSecondsRef.current = 0;
    completedRef.current = false;
    setTimerSettings((current) => ({ ...current, countUpEnabled: enabled }));
  };

  const reorderContextComponents = (sourceId, targetId) => {
    if (!sourceId || sourceId === targetId) return;
    setContextLayout((current) => {
      const order = [...current.order];
      const sourceIndex = order.indexOf(sourceId);
      const targetIndex = order.indexOf(targetId);
      if (sourceIndex < 0 || targetIndex < 0) return current;
      order.splice(sourceIndex, 1);
      order.splice(targetIndex, 0, sourceId);
      return { ...current, order };
    });
  };

  const nudgeContextComponent = (id, direction) => {
    setContextLayout((current) => {
      const order = [...current.order];
      const index = order.indexOf(id);
      const nextIndex = Math.min(order.length - 1, Math.max(0, index + direction));
      if (index < 0 || index === nextIndex) return current;
      [order[index], order[nextIndex]] = [order[nextIndex], order[index]];
      return { ...current, order };
    });
  };

  const toggleContextVisibility = (id) => {
    setContextLayout((current) => ({
      ...current,
      visible: { ...current.visible, [id]: !current.visible[id] },
    }));
  };

  const copyCurrentQuote = async () => {
    const quote = remoteQuote || FOCUS_QUOTES[quoteIndex];
    try {
      await copyTextToClipboard(quote);
      showTopMessage("名言已复制", quote, false, "focus", false);
    } catch {
      showTopMessage("复制失败", "当前环境暂时不允许访问剪贴板。", false, "warning", false);
    }
  };

  const showQuickNote = (note) => {
    if (!note) return;
    const nextPosition = clampQuickNotePosition(note.position || defaultQuickNotePosition());
    quickNotePositionRef.current = nextPosition;
    setQuickNotePosition(nextPosition);
    setOpenQuickNoteId(note.id);
    setActiveUtilityWindow("note");
  };

  const createQuickNote = () => {
    const now = new Date().toISOString();
    const note = {
      id: `note-${Date.now()}`,
      content: "",
      createdAt: now,
      updatedAt: now,
      position: defaultQuickNotePosition(),
    };
    setQuickNotes((current) => [note, ...current]);
    showQuickNote(note);
  };

  const saveQuickNotePosition = (noteId, position = quickNotePositionRef.current) => {
    if (!noteId) return;
    setQuickNotes((current) => current.map((note) => (
      note.id === noteId ? { ...note, position } : note
    )));
  };

  const closeQuickNote = () => {
    window.clearTimeout(quickNoteDeleteTimerRef.current);
    setDeletingQuickNoteId(null);
    saveQuickNotePosition(openQuickNoteId);
    setOpenQuickNoteId(null);
  };

  const updateQuickNote = (content) => {
    const updatedAt = new Date().toISOString();
    setQuickNotes((current) => current.map((note) => (
      note.id === openQuickNoteId ? { ...note, content, updatedAt } : note
    )));
  };

  const startQuickNoteDrag = (event) => {
    if (event.target.closest("button")) return;
    setActiveUtilityWindow("note");
    quickNoteDragRef.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      originX: quickNotePositionRef.current.x,
      originY: quickNotePositionRef.current.y,
    };
    event.currentTarget.setPointerCapture?.(event.pointerId);
    event.preventDefault();
  };

  const moveQuickNote = (event) => {
    const drag = quickNoteDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;
    const nextPosition = clampQuickNotePosition({
      x: drag.originX + event.clientX - drag.startX,
      y: drag.originY + event.clientY - drag.startY,
    });
    quickNotePositionRef.current = nextPosition;
    setQuickNotePosition(nextPosition);
  };

  const endQuickNoteDrag = (event) => {
    const drag = quickNoteDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;
    quickNoteDragRef.current = null;
    event.currentTarget.releasePointerCapture?.(event.pointerId);
    saveQuickNotePosition(openQuickNoteId, quickNotePositionRef.current);
  };

  const startTranslatorDrag = (event) => {
    if (event.target.closest("button, a")) return;
    setActiveUtilityWindow("translator");
    translatorDragRef.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      originX: translatorPositionRef.current.x,
      originY: translatorPositionRef.current.y,
    };
    event.currentTarget.setPointerCapture?.(event.pointerId);
    event.preventDefault();
  };

  const moveTranslator = (event) => {
    const drag = translatorDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;
    const nextPosition = clampTranslatorPosition({
      x: drag.originX + event.clientX - drag.startX,
      y: drag.originY + event.clientY - drag.startY,
    });
    translatorPositionRef.current = nextPosition;
    setTranslatorPosition(nextPosition);
  };

  const endTranslatorDrag = (event) => {
    const drag = translatorDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;
    translatorDragRef.current = null;
    event.currentTarget.releasePointerCapture?.(event.pointerId);
  };

  const startCalculatorDrag = (event) => {
    if (event.target.closest("button")) return;
    setActiveUtilityWindow("calculator");
    calculatorDragRef.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      originX: calculatorPositionRef.current.x,
      originY: calculatorPositionRef.current.y,
    };
    event.currentTarget.setPointerCapture?.(event.pointerId);
    event.preventDefault();
  };

  const moveCalculator = (event) => {
    const drag = calculatorDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;
    const nextPosition = clampCalculatorPosition({
      x: drag.originX + event.clientX - drag.startX,
      y: drag.originY + event.clientY - drag.startY,
    });
    calculatorPositionRef.current = nextPosition;
    setCalculatorPosition(nextPosition);
  };

  const endCalculatorDrag = (event) => {
    const drag = calculatorDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;
    calculatorDragRef.current = null;
    event.currentTarget.releasePointerCapture?.(event.pointerId);
  };

  const startStopwatchDrag = (event) => {
    if (event.target.closest("button")) return;
    setActiveUtilityWindow("stopwatch");
    stopwatchDragRef.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      originX: stopwatchPositionRef.current.x,
      originY: stopwatchPositionRef.current.y,
    };
    event.currentTarget.setPointerCapture?.(event.pointerId);
    event.preventDefault();
  };

  const moveStopwatch = (event) => {
    const drag = stopwatchDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;
    const nextPosition = clampStopwatchPosition({
      x: drag.originX + event.clientX - drag.startX,
      y: drag.originY + event.clientY - drag.startY,
    });
    stopwatchPositionRef.current = nextPosition;
    setStopwatchPosition(nextPosition);
  };

  const endStopwatchDrag = (event) => {
    const drag = stopwatchDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;
    stopwatchDragRef.current = null;
    event.currentTarget.releasePointerCapture?.(event.pointerId);
  };

  const clearCalculator = () => {
    setCalculatorDisplay("0");
    setCalculatorAccumulator(null);
    setCalculatorOperator(null);
    setCalculatorWaiting(false);
    setCalculatorExpression("");
  };

  const inputCalculatorDigit = (digit) => {
    if (calculatorDisplay === "错误" || calculatorWaiting) {
      setCalculatorDisplay(digit === "." ? "0." : digit);
      setCalculatorWaiting(false);
      return;
    }
    if (digit === ".") {
      if (!calculatorDisplay.includes(".")) setCalculatorDisplay(`${calculatorDisplay}.`);
      return;
    }
    setCalculatorDisplay((current) => current === "0" ? digit : `${current}${digit}`.slice(0, 14));
  };

  const chooseCalculatorOperator = (nextOperator) => {
    const inputValue = Number(calculatorDisplay);
    if (!Number.isFinite(inputValue)) {
      clearCalculator();
      return;
    }
    if (calculatorOperator && calculatorAccumulator != null && !calculatorWaiting) {
      const result = runBasicCalculation(calculatorAccumulator, calculatorOperator, inputValue);
      const formatted = formatCalculatorValue(result);
      setCalculatorDisplay(formatted);
      setCalculatorAccumulator(Number.isFinite(result) ? result : null);
      setCalculatorExpression(Number.isFinite(result) ? `${formatted} ${nextOperator}` : "");
    } else {
      setCalculatorAccumulator(inputValue);
      setCalculatorExpression(`${calculatorDisplay} ${nextOperator}`);
    }
    setCalculatorOperator(nextOperator);
    setCalculatorWaiting(true);
  };

  const resolveCalculator = () => {
    if (!calculatorOperator || calculatorAccumulator == null) return;
    const inputValue = Number(calculatorDisplay);
    const result = runBasicCalculation(calculatorAccumulator, calculatorOperator, inputValue);
    setCalculatorExpression(`${formatCalculatorValue(calculatorAccumulator)} ${calculatorOperator} ${calculatorDisplay} =`);
    setCalculatorDisplay(formatCalculatorValue(result));
    setCalculatorAccumulator(null);
    setCalculatorOperator(null);
    setCalculatorWaiting(true);
  };

  const applyCalculatorAction = (action) => {
    if (/^\d$/.test(action) || action === ".") inputCalculatorDigit(action);
    else if (["+", "−", "×", "÷"].includes(action)) chooseCalculatorOperator(action);
    else if (action === "=") resolveCalculator();
    else if (action === "clear") clearCalculator();
    else if (action === "sign") setCalculatorDisplay((current) => current === "0" || current === "错误" ? current : current.startsWith("-") ? current.slice(1) : `-${current}`);
    else if (action === "percent") setCalculatorDisplay((current) => formatCalculatorValue(Number(current) / 100));
    else if (action === "backspace") setCalculatorDisplay((current) => current === "错误" || current.length <= 1 ? "0" : current.slice(0, -1));
  };

  useEffect(() => {
    if (!calculatorOpen) return undefined;
    const handleCalculatorKey = (event) => {
      if (event.ctrlKey || event.metaKey || event.altKey || event.target.closest?.("input, textarea, [contenteditable='true']")) return;
      const keyMap = { "/": "÷", "*": "×", "-": "−", "+": "+", Enter: "=", "=": "=", Backspace: "backspace", Delete: "clear", "%": "percent" };
      const action = /^\d$/.test(event.key) || event.key === "." ? event.key : keyMap[event.key];
      if (!action) return;
      event.preventDefault();
      applyCalculatorAction(action);
    };
    window.addEventListener("keydown", handleCalculatorKey);
    return () => window.removeEventListener("keydown", handleCalculatorKey);
  }, [calculatorOpen, calculatorDisplay, calculatorAccumulator, calculatorOperator, calculatorWaiting]);

  useEffect(() => {
    if (!stopwatchRunning) return undefined;
    stopwatchStartedAtRef.current = performance.now() - stopwatchElapsedMs;
    let animationFrame = 0;
    const updateStopwatch = () => {
      setStopwatchElapsedMs(performance.now() - stopwatchStartedAtRef.current);
      animationFrame = window.requestAnimationFrame(updateStopwatch);
    };
    animationFrame = window.requestAnimationFrame(updateStopwatch);
    return () => window.cancelAnimationFrame(animationFrame);
  }, [stopwatchRunning]);

  const toggleStopwatch = () => {
    if (stopwatchRunning) {
      setStopwatchElapsedMs(performance.now() - stopwatchStartedAtRef.current);
      setStopwatchRunning(false);
      return;
    }
    setStopwatchRunning(true);
  };

  const stopStopwatch = () => {
    if (stopwatchRunning) setStopwatchElapsedMs(performance.now() - stopwatchStartedAtRef.current);
    setStopwatchRunning(false);
  };

  const addStopwatchLap = () => {
    const total = stopwatchRunning ? performance.now() - stopwatchStartedAtRef.current : stopwatchElapsedMs;
    if (total < 10) return;
    setStopwatchLaps((current) => {
      const previousTotal = current[0]?.total || 0;
      return [{ id: `${Date.now()}-${current.length}`, total, delta: Math.max(0, total - previousTotal) }, ...current];
    });
  };

  const clearStopwatch = () => {
    setStopwatchRunning(false);
    setStopwatchElapsedMs(0);
    setStopwatchLaps([]);
    stopwatchStartedAtRef.current = 0;
  };

  const startQuickNoteDelete = (event, noteId) => {
    if (!noteId || event.button !== 0) return;
    event.preventDefault();
    event.stopPropagation();
    window.clearTimeout(quickNoteDeleteTimerRef.current);
    setDeletingQuickNoteId(noteId);
    quickNoteDeleteTimerRef.current = window.setTimeout(() => {
      setQuickNotes((current) => current.filter((note) => note.id !== noteId));
      setOpenQuickNoteId((current) => (current === noteId ? null : current));
      setDeletingQuickNoteId(null);
    }, QUICK_NOTE_DELETE_HOLD_MS);
    try {
      event.currentTarget.setPointerCapture?.(event.pointerId);
    } catch {
      // Pointer capture can be unavailable in embedded browser surfaces.
    }
  };

  const cancelQuickNoteDelete = (event) => {
    if (!deletingQuickNoteId) return;
    window.clearTimeout(quickNoteDeleteTimerRef.current);
    setDeletingQuickNoteId(null);
    if (event?.pointerId != null) {
      try {
        event.currentTarget.releasePointerCapture?.(event.pointerId);
      } catch {
        // It may already be released when the pointer leaves the control.
      }
    }
  };

  const selectAppearanceMode = (nextMode) => {
    setAppearanceMode(nextMode);
    setTheme(nextMode === "auto" ? resolveAutomaticTheme(new Date(), sunTimes) : nextMode);
  };

  const openMiniWindow = async () => {
    const modeLabel = countUpEnabled ? "正向计时" : MODES[mode].label;
    const runningLabel = isRunning ? "暂停" : "继续";
    const time = `${minuteText}:${secondText}`;
    const miniProgress = Math.round(Math.min(1, Math.max(0, progress)) * 100);

    if (window.chrome?.webview?.postMessage) {
      setMiniWindowMode(true);
      window.chrome.webview.postMessage(`mini-window|show|${modeLabel}|${runningLabel}|${time}|${miniProgress}`);
      return;
    }

    if (!("documentPictureInPicture" in window)) {
      showTopMessage("当前环境不支持小窗", "请使用支持文档画中画的浏览器，或安装 Tomatotodo Windows 版。", false, "warning", false);
      return;
    }

    try {
      const miniWindow = await window.documentPictureInPicture.requestWindow({ width: 300, height: 168 });
      miniWindowRef.current = miniWindow;
      miniWindow.document.title = "Tomatotodo 小窗";
      miniWindow.document.body.innerHTML = `
        <main class="mini-shell">
          <div class="mini-heading"><strong id="mini-mode"></strong><button id="mini-status" type="button"></button></div>
          <time id="mini-time"></time>
          <small>Tomatotodo</small>
        </main>
      `;
      const style = miniWindow.document.createElement("style");
      style.textContent = `
        :root { color-scheme: dark; --mini-bg:#0b110d; --mini-surface:#142018; --mini-text:#f5f4ed; --mini-muted:#9aa69d; --mini-accent:#7ca982; }
        * { box-sizing:border-box; }
        html, body { width:100%; height:100%; margin:0; overflow:hidden; }
        body { padding:12px; color:var(--mini-text); background:var(--mini-bg); font-family:"Segoe UI","Noto Sans SC",sans-serif; }
        .mini-shell { display:flex; width:100%; height:100%; flex-direction:column; justify-content:space-between; padding:15px 18px 13px; border:1px solid color-mix(in srgb,var(--mini-accent) 30%,transparent); border-radius:22px; background:color-mix(in srgb,var(--mini-surface) 86%,transparent); box-shadow:inset 0 1px rgba(255,255,255,.12),0 16px 44px rgba(0,0,0,.28); }
        .mini-heading { display:flex; align-items:center; justify-content:space-between; gap:10px; }
        .mini-heading strong { color:var(--mini-accent); font-size:14px; letter-spacing:.12em; }
        .mini-heading button { position:relative; width:70px; min-height:24px; margin:-3px -4px -3px 0; overflow:hidden; padding:6px 9px; border:1px solid color-mix(in srgb,var(--mini-accent) 16%,transparent); border-radius:999px; color:var(--mini-muted); background:linear-gradient(90deg,color-mix(in srgb,var(--mini-accent) 16%,transparent) 0 var(--mini-progress,0%),color-mix(in srgb,var(--mini-text) 5%,transparent) var(--mini-progress,0%) 100%); font:500 10px/1 "Segoe UI","Noto Sans SC",sans-serif; cursor:pointer; transition:color .2s ease,border-color .2s ease,transform .16s ease; }
        .mini-heading button[data-running="true"] { color:color-mix(in srgb,var(--mini-accent) 54%,var(--mini-text)); }
        .mini-heading button:hover { color:var(--mini-text); border-color:color-mix(in srgb,var(--mini-accent) 30%,transparent); }
        .mini-heading button:active { transform:scale(.94); }
        .mini-heading button:focus-visible { outline:1px solid var(--mini-accent); outline-offset:2px; }
        time { font-family:"Bodoni MT",Didot,"Times New Roman",serif; font-size:48px; line-height:1; letter-spacing:.02em; font-variant-numeric:tabular-nums; }
        small { color:var(--mini-muted); font-size:9px; letter-spacing:.16em; text-transform:uppercase; }
      `;
      miniWindow.document.head.appendChild(style);
      const statusButton = miniWindow.document.getElementById("mini-status");
      if (statusButton) {
        statusButton.textContent = runningLabel;
        statusButton.dataset.running = isRunning ? "true" : "false";
        statusButton.style.setProperty("--mini-progress", `${miniProgress}%`);
        statusButton.setAttribute("aria-label", isRunning ? "暂停计时" : "继续计时");
        statusButton.addEventListener("click", () => toggleTimerRef.current?.());
      }
      miniWindow.addEventListener("pagehide", () => {
        miniWindowRef.current = null;
        setMiniWindowMode(false);
      }, { once: true });
      setMiniWindowMode(true);
    } catch {
      showTopMessage("小窗未能打开", "浏览器取消了画中画窗口，您可以稍后重新开启。", false, "warning", false);
    }
  };

  const closeMiniWindow = () => {
    setMiniWindowMode(false);
    if (window.chrome?.webview?.postMessage) {
      window.chrome.webview.postMessage("mini-window|hide");
      return;
    }
    if (miniWindowRef.current && !miniWindowRef.current.closed) miniWindowRef.current.close();
    miniWindowRef.current = null;
  };

  const toggleMiniWindowMode = (enabled) => {
    if (enabled) openMiniWindow();
    else closeMiniWindow();
  };

  const sunTimeCopy = sunTimes
    ? `${sunTimes.sunrise.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit", hour12: false })} 日出 · ${sunTimes.sunset.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit", hour12: false })} 日落`
    : "正在获取当地日出与日落时间";

  const completedTasks = tasks.filter((task) => task.done).length;

  const renderDashboardWidget = (widgetId) => {
    if (widgetId === "timer") {
      const timerTaskLabel = activeTask
        ? `${activeTask.title}${activeTask.subtitle?.trim() ? ` · ${activeTask.subtitle.trim()}` : ""}`
        : "选择一项任务，再开始专注";
      return (
        <div className="dashboard-timer-widget">
          <div className="dashboard-widget-kicker"><Timer weight="duotone" /><span>{countUpEnabled ? "正向记录" : mode === "focus" ? "番茄专注" : "短休恢复"}</span></div>
          <div className="dashboard-timer-row">
            <strong aria-label={`${minuteText}分${secondText}秒`}><span>{minuteText}</span><b>:</b><span>{secondText}</span></strong>
            <div className="dashboard-timer-status">
              <i className={isRunning ? "is-running" : ""} />
              <span>{isRunning ? "正在进行" : "已暂停"}</span>
            </div>
          </div>
          <div className="dashboard-timer-track" aria-hidden="true"><span style={{ width: `${countUpEnabled ? 100 : Math.max(0, Math.min(100, progress * 100))}%` }} /></div>
          <small title={timerTaskLabel}>{timerTaskLabel}</small>
        </div>
      );
    }

    if (widgetId === "mode") {
      return (
        <div className={`dashboard-mode-widget ${countUpEnabled ? "is-disabled" : ""}`}>
          <div className="dashboard-widget-kicker"><Clock weight="duotone" /><span>计划时段</span></div>
          <div className={`dashboard-segmented-control ${mode === "short" ? "is-short" : "is-focus"}`} role="group" aria-label="选择计划时段">
            <button className={mode === "focus" ? "is-active" : ""} type="button" onClick={() => chooseMode("focus")} disabled={countUpEnabled} aria-pressed={mode === "focus"}>
              <span>专注</span>
              <small>{timerSettings.focus} 分钟</small>
            </button>
            <button className={mode === "short" ? "is-active" : ""} type="button" onClick={() => chooseMode("short")} disabled={countUpEnabled || !timerSettings.shortBreakEnabled} aria-pressed={mode === "short"}>
              <span>短休</span>
              <small>{timerSettings.short} 分钟</small>
            </button>
          </div>
        </div>
      );
    }

    if (widgetId === "countup") {
      return (
        <label className="dashboard-switch-widget dashboard-compact-switch-widget">
          <span className="dashboard-widget-kicker"><TrendUp weight="duotone" /><span>正向计时</span></span>
          <span className="dashboard-switch-copy"><input type="checkbox" checked={countUpEnabled} onChange={(event) => toggleCountUpMode(event.target.checked)} /><i aria-hidden="true" /></span>
        </label>
      );
    }

    if (widgetId === "immersive") {
      return (
        <label className="dashboard-switch-widget dashboard-compact-switch-widget">
          <span className="dashboard-widget-kicker"><CornersOut weight="duotone" /><span>沉浸模式</span></span>
          <span className="dashboard-switch-copy"><input type="checkbox" checked={immersive} onChange={(event) => setImmersive(event.target.checked)} /><i aria-hidden="true" /></span>
        </label>
      );
    }

    if (widgetId === "miniwindow") {
      return (
        <label className="dashboard-switch-widget dashboard-compact-switch-widget">
          <span className="dashboard-widget-kicker"><PictureInPicture weight="duotone" /><span>小窗模式</span></span>
          <span className="dashboard-switch-copy"><input type="checkbox" checked={miniWindowMode} onChange={(event) => toggleMiniWindowMode(event.target.checked)} /><i aria-hidden="true" /></span>
        </label>
      );
    }

    if (widgetId === "tasks") {
      return (
        <div className="dashboard-tasks-widget">
          <header><span className="dashboard-widget-kicker"><ListChecks weight="duotone" /><span>{activeListName}</span></span><strong>{completedTasks}<small> / {tasks.length}</small></strong></header>
          <div className="dashboard-task-list">
            {tasks.map((task) => (
              <div className={`${task.id === activeTaskId ? "is-active" : ""} ${task.done ? "is-done" : ""}`} key={task.id}>
                <button type="button" onClick={() => selectTask(task.id)} title={task.title}>
                  <i aria-hidden="true" />
                  <span>{task.title}</span>
                  {task.estimatedPomodoros ? <small className="dashboard-task-estimate">{task.estimatedPomodoros} 番茄</small> : null}
                </button>
                <button type="button" onClick={() => toggleDone(task.id)} aria-label={task.done ? `取消完成：${task.title}` : `完成任务：${task.title}`}>{task.done ? <Check weight="bold" /> : null}</button>
              </div>
            ))}
            {!tasks.length ? <p>清单还是空的</p> : null}
          </div>
          <footer><span>已完成 {completedTasks}/{tasks.length}</span><button type="button" onClick={showConfigurationView}>管理清单<ArrowRight /></button></footer>
        </div>
      );
    }

    if (widgetId === "quote") {
      const quote = remoteQuote || FOCUS_QUOTES[quoteIndex];
      return (
        <button className="dashboard-quote-widget" type="button" onClick={copyCurrentQuote} aria-label={`复制名言：${quote}`}>
          <span className="dashboard-widget-kicker"><NotePencil weight="duotone" /><span>名言警句</span></span>
          <strong>“{quote}”</strong><small>点击复制</small>
        </button>
      );
    }

    if (widgetId === "active") {
      return (
        <div className="dashboard-active-widget">
          <span className="dashboard-widget-kicker"><Target weight="duotone" /><span>正在进行</span></span>
          <strong title={activeTask?.title}>{activeTask?.title || "尚未分配任务"}</strong>
          <small>{activeTask?.estimatedPomodoros ? `预计 ${activeTask.estimatedPomodoros} 个番茄钟` : "从任务清单中选择一项"}</small>
        </div>
      );
    }

    if (widgetId === "calendar") {
      return (
        <div className="dashboard-calendar-widget">
          <header><span className="dashboard-widget-kicker"><CalendarBlank weight="duotone" /><span>日历</span></span><strong>{contextCalendarMonth}</strong></header>
          <div className="dashboard-calendar-weekdays" aria-hidden="true">{["一", "二", "三", "四", "五", "六", "日"].map((day) => <span key={day}>{day}</span>)}</div>
          <div className="dashboard-calendar-days">
            {contextCalendarDays.map((day) => {
              const key = toDateKey(day);
              const outside = day.getMonth() !== currentTime.getMonth();
              return <time className={`${key === todayKey ? "is-today" : ""} ${outside ? "is-outside" : ""}`} dateTime={key} key={key}>{day.getDate()}</time>;
            })}
          </div>
        </div>
      );
    }

    if (widgetId === "analogclock") {
      return (
        <div className="dashboard-analog-clock-widget">
          <span className="dashboard-widget-kicker"><Clock weight="duotone" /><span>圆表</span></span>
          <div className="dashboard-analog-clock-face" role="img" aria-label={`当前时间 ${clockCopy}`}>
            <svg className="dashboard-analog-clock-dial" viewBox="0 0 200 200" preserveAspectRatio="xMidYMid meet" aria-hidden="true">
              <circle className="analog-clock-ring" cx="100" cy="100" r="88" />
              {analogClockSettings.showMarkers ? Array.from({ length: 12 }, (_, index) => (
                <line className={`analog-clock-marker ${index % 3 === 0 ? "is-major" : ""}`} x1="100" y1="20" x2="100" y2={index % 3 === 0 ? "31" : "27"} transform={`rotate(${index * 30} 100 100)`} key={index} />
              )) : null}
              <g transform={`rotate(${analogHourAngle} 100 100)`}>
                <line className="analog-clock-hand is-hour" x1="100" y1="108" x2="100" y2="58" />
              </g>
              <g transform={`rotate(${analogMinuteAngle} 100 100)`}>
                <line className="analog-clock-hand is-minute" x1="100" y1="110" x2="100" y2="38" />
              </g>
              {analogClockSettings.showSeconds ? (
                <g transform={`rotate(${analogSecondAngle} 100 100)`}>
                  <line className="analog-clock-hand is-second" x1="100" y1="116" x2="100" y2="30" />
                </g>
              ) : null}
              <circle className="analog-clock-pin" cx="100" cy="100" r="5" />
            </svg>
          </div>
        </div>
      );
    }

    if (widgetId === "media") {
      const playbackModeLabel = localPlaybackMode === "repeat-one" ? "单曲循环" : localPlaybackMode === "shuffle" ? "随机播放" : "顺序播放";
      return (
        <div className="dashboard-media-widget">
          <div className="dashboard-media-content">
            <div className="dashboard-media-art">{displayedMedia.artwork ? <img src={displayedMedia.artwork} alt="" /> : <MusicNotes weight="duotone" />}</div>
            <div className={`dashboard-media-stage ${mediaSource === "local" && currentLocalTrack ? "has-controls" : ""}`} tabIndex={mediaSource === "local" && currentLocalTrack ? 0 : undefined}>
              <div className="dashboard-media-copy"><strong title={displayedMedia.title}>{displayedMedia.title}</strong><small title={displayedMedia.artist}>{displayedMedia.artist || (mediaSource === "local" ? "本地音乐" : "Spotify")}</small></div>
              {mediaSource === "local" && currentLocalTrack ? (
                <div className="dashboard-local-media-controls">
                  <input
                    className="dashboard-local-media-progress"
                    type="range"
                    min="0"
                    max={Math.max(localDuration, 0)}
                    step="0.1"
                    value={Math.min(localCurrentTime, localDuration || 0)}
                    onChange={seekLocalTrack}
                    style={{ "--media-progress": `${localDuration ? (localCurrentTime / localDuration) * 100 : 0}%` }}
                    aria-label="音乐播放进度"
                  />
                  <div className="dashboard-local-media-actions">
                    <button type="button" onClick={() => changeLocalTrack(-1)} aria-label="上一首"><SkipBack weight="fill" /></button>
                    <button className="is-primary" type="button" onClick={toggleLocalPlayback} aria-label={localPlaying ? "暂停" : "播放"}>{localPlaying ? <Pause weight="fill" /> : <Play weight="fill" />}</button>
                    <button type="button" onClick={() => changeLocalTrack(1)} aria-label="下一首"><SkipForward weight="fill" /></button>
                    <button className="dashboard-local-media-mode" type="button" onClick={cycleLocalPlaybackMode} aria-label={`${playbackModeLabel}，点击切换播放模式`}>
                      {localPlaybackMode === "shuffle" ? <Shuffle weight="bold" /> : localPlaybackMode === "repeat-one" ? <Repeat weight="bold" /> : <List weight="bold" />}
                      {localPlaybackMode === "repeat-one" ? <b aria-hidden="true">1</b> : null}
                    </button>
                    <div className="dashboard-local-media-volume">
                      <button className="dashboard-local-media-mute" type="button" onClick={toggleLocalMute} aria-label={localMuted ? "恢复声音" : "静音"}>{localMuted ? <SpeakerSlash aria-hidden="true" /> : <SpeakerHigh aria-hidden="true" />}</button>
                      <input
                        type="range"
                        min="0"
                        max="1"
                        step="0.01"
                        value={localMuted ? 0 : localVolume}
                        onChange={changeLocalVolume}
                        style={{ "--media-progress": `${localMuted ? 0 : localVolume * 100}%` }}
                        aria-label="音量"
                      />
                    </div>
                  </div>
                </div>
              ) : null}
            </div>
          </div>
        </div>
      );
    }

    if (widgetId === "date") {
      return (
        <div className="dashboard-date-widget">
          <span className="dashboard-widget-kicker"><Clock weight="duotone" /><span>日期与时间</span></span>
          <div><strong>{clockCopy}</strong><span><b>{dateCopy.numeric}</b>{dateCopy.weekday}</span></div>
        </div>
      );
    }

    if (widgetId === "weather") {
      return (
        <div className="dashboard-weather-widget">
          <span className="dashboard-widget-kicker"><CloudSun weight="duotone" /><span>实时天气</span></span>
          <div className="dashboard-weather-content">
            <div className="dashboard-weather-copy"><strong>{weather.description}</strong><small><MapPin weight="fill" />{weather.location}</small></div>
            <b>{Number.isFinite(weather.temperature) ? `${weather.temperature}°` : "--"}</b>
          </div>
        </div>
      );
    }

    return (
      <div className="dashboard-countdown-widget">
        <span className="dashboard-widget-kicker"><CalendarBlank weight="duotone" /><span>倒数日</span></span>
        <div className="dashboard-countdown-content">
          <div className="dashboard-countdown-copy"><small>距离 <strong>{countdownLabel}</strong> 还有</small></div>
          <strong>{countdownRemainingDays}<small> 天</small></strong>
        </div>
      </div>
    );
  };

  return (
    <main
      className={`app-shell ${immersive ? "is-immersive" : ""} ${countUpEnabled ? "is-count-up" : ""} ${pureBlack && theme === "dark" ? "is-pure-black" : ""}`}
      data-theme={theme}
      data-font-scale-enabled={fontScaleEnabled ? "true" : "false"}
      style={{ ...monetTokens, "--font-scale-factor": fontScaleEnabled ? fontScale / 100 : 1 }}
    >
      {immersive ? (
        <section className={`immersive-clock-screen is-${immersivePhase}`} aria-label={`沉浸计时器，${immersivePhaseLabel}`}>
          <button
            className="immersive-dynamic-island"
            type="button"
            onClick={toggleTimer}
            aria-label={`${immersivePhaseLabel}，点击${isRunning ? "暂停" : "继续"}计时`}
          >
            <span className="immersive-status-dot" aria-hidden="true" />
            <strong aria-live="polite">{immersivePhaseLabel}</strong>
            <span>{countUpEnabled ? "正向计时" : mode === "short" ? "短休" : "Tomatotodo"}</span>
            {isRunning ? <Pause weight="fill" aria-hidden="true" /> : <Play weight="fill" aria-hidden="true" />}
          </button>

          <div className="immersive-flip-stage" role="timer" aria-live="off" aria-label={`${immersiveClockParts[0].value}小时${immersiveClockParts[1].value}分${immersiveClockParts[2].value}秒`}>
            {immersiveClockParts.map((part) => (
              <div className="immersive-flip-card" key={part.key} aria-label={`${part.value}${part.label}`}>
                <span className="immersive-flip-value" key={`${part.key}-${part.value}`}>{part.value}</span>
              </div>
            ))}
          </div>

          <button className="immersive-exit-button" type="button" onClick={() => setImmersive(false)} aria-label="退出沉浸模式" title="退出沉浸模式">
            <CornersIn weight="bold" aria-hidden="true" />
          </button>
        </section>
      ) : null}

      {mediaSource === "local" && currentLocalTrack ? (
        <audio
          className="global-local-audio"
          ref={localAudioRef}
          src={currentLocalTrack.url}
          preload="metadata"
          onLoadedMetadata={(event) => {
            event.currentTarget.volume = localVolume;
            event.currentTarget.muted = localMuted;
            setLocalDuration(Number.isFinite(event.currentTarget.duration) ? event.currentTarget.duration : 0);
            if (localAutoplayRef.current) {
              localAutoplayRef.current = false;
              event.currentTarget.play().catch(() => {});
            }
          }}
          onTimeUpdate={(event) => setLocalCurrentTime(event.currentTarget.currentTime)}
          onPlay={() => setLocalPlaying(true)}
          onPause={() => setLocalPlaying(false)}
          onEnded={handleLocalTrackEnded}
        />
      ) : null}

      <nav className={`md3-side-rail ${railCompact ? "is-compact" : ""}`} aria-label="主要功能">
        <button
          className="md3-rail-brand"
          type="button"
          onClick={showDailyFortune}
          disabled={logoCoolingDown}
          onDragStart={(event) => event.preventDefault()}
          aria-label={logoCoolingDown ? "Tomatotodo，劝勉冷却中" : "Tomatotodo，点击查看今日运势"}
          title={logoCoolingDown ? "稍后再试" : "今日运势"}
        >
          <img src="/icons/icon-192.png" alt="" aria-hidden="true" draggable="false" />
          <span>TT</span>
        </button>
        <button
          className={`md3-rail-button ${!settingsOpen && !dashboardVisible && !toolsOpen ? "is-active" : ""}`}
          type="button"
          onClick={showDashboardView}
          aria-label="打开仪表盘"
          aria-current={!settingsOpen && !dashboardVisible && !toolsOpen ? "page" : undefined}
        >
          <Timer weight="duotone" aria-hidden="true" />
          <span>仪表盘</span>
        </button>

        <button
          className={`md3-rail-button ${settingsOpen && settingsTab === "tasks" ? "is-active" : ""}`}
          type="button"
          onClick={showConfigurationView}
          aria-label="打开配置"
          aria-current={settingsOpen && settingsTab === "tasks" ? "page" : undefined}
        >
          <ListChecks weight="duotone" aria-hidden="true" />
          <span>配置</span>
        </button>

        <button
          className={`md3-rail-button ${settingsOpen && settingsTab === "personalization" ? "is-active" : ""}`}
          type="button"
          onClick={showPersonalizationView}
          aria-label="打开个性化"
          aria-current={settingsOpen && settingsTab === "personalization" ? "page" : undefined}
        >
          <Palette weight="duotone" aria-hidden="true" />
          <span>个性化</span>
        </button>

        <button
          className={`md3-rail-button ${settingsOpen && settingsTab === "general" ? "is-active" : ""}`}
          type="button"
          onClick={showGeneralView}
          aria-label="打开常规"
          aria-current={settingsOpen && settingsTab === "general" ? "page" : undefined}
        >
          <SlidersHorizontal weight="duotone" aria-hidden="true" />
          <span>常规</span>
        </button>

        <button
          className={`md3-rail-button md3-archive-button ${dashboardVisible ? "is-active" : ""}`}
          type="button"
          onClick={showArchiveView}
          aria-label="打开专注档案"
          aria-current={dashboardVisible ? "page" : undefined}
        >
          <ChartBar weight="duotone" aria-hidden="true" />
          <span>档案</span>
        </button>

        <button
          className={`md3-rail-button ${toolsOpen ? "is-active" : ""}`}
          type="button"
          onClick={showToolsView}
          aria-label="打开工具"
          aria-current={toolsOpen ? "page" : undefined}
        >
          <Wrench weight="duotone" aria-hidden="true" />
          <span>工具</span>
        </button>
        <div className="md3-rail-spacer" />
        <button
          className="md3-rail-toggle"
          type="button"
          onClick={() => setRailCompact((current) => !current)}
          aria-label={railCompact ? "显示导航文字" : "隐藏导航文字"}
          aria-pressed={railCompact}
          title={railCompact ? "显示导航文字" : "隐藏导航文字"}
        >
          <List weight="bold" aria-hidden="true" />
        </button>
      </nav>

      {translatorOpen ? (
        <aside
          className={`translator-shell ${activeUtilityWindow === "translator" ? "is-front" : ""}`}
          role="dialog"
          aria-modal="false"
          aria-label="必应翻译"
          onPointerDownCapture={() => setActiveUtilityWindow("translator")}
          onFocusCapture={() => setActiveUtilityWindow("translator")}
          style={{ left: `${translatorPosition.x}px`, top: `${translatorPosition.y}px` }}
        >
          <div className="translator-glass">
            <div className="translator-card">
              <header
                className="translator-header"
                onPointerDown={startTranslatorDrag}
                onPointerMove={moveTranslator}
                onPointerUp={endTranslatorDrag}
                onPointerCancel={endTranslatorDrag}
              >
                <span><Translate weight="light" aria-hidden="true" /><strong>翻译</strong><small>必应翻译</small></span>
                <div>
                  <a href={BING_TRANSLATOR_URL} target="_blank" rel="noreferrer" aria-label="在浏览器中打开必应翻译" title="外部打开"><ArrowSquareOut aria-hidden="true" /></a>
                  <button type="button" onClick={() => setTranslatorOpen(false)} aria-label="关闭翻译"><X aria-hidden="true" /></button>
                </div>
              </header>
              <div className="translator-frame-shell">
                <iframe
                  src={BING_TRANSLATOR_URL}
                  title="必应翻译"
                  loading="lazy"
                  referrerPolicy="strict-origin-when-cross-origin"
                />
                <div className="translator-frame-fallback">
                  <Translate aria-hidden="true" />
                  <strong>必应翻译未能嵌入</strong>
                  <span>部分浏览器会阻止第三方页面显示在小窗中。</span>
                  <a href={BING_TRANSLATOR_URL} target="_blank" rel="noreferrer">在浏览器中打开</a>
                </div>
              </div>
            </div>
          </div>
        </aside>
      ) : null}

      {calculatorOpen ? (
        <aside
          className={`calculator-shell ${activeUtilityWindow === "calculator" ? "is-front" : ""}`}
          role="dialog"
          aria-modal="false"
          aria-label="计算器"
          onPointerDownCapture={() => setActiveUtilityWindow("calculator")}
          onFocusCapture={() => setActiveUtilityWindow("calculator")}
          style={{ left: `${calculatorPosition.x}px`, top: `${calculatorPosition.y}px` }}
        >
          <div className="calculator-glass">
            <header
              className="calculator-header"
              onPointerDown={startCalculatorDrag}
              onPointerMove={moveCalculator}
              onPointerUp={endCalculatorDrag}
              onPointerCancel={endCalculatorDrag}
            >
              <span><Calculator weight="light" aria-hidden="true" /><strong>计算器</strong><small>基础计算</small></span>
              <button type="button" onClick={() => setCalculatorOpen(false)} aria-label="关闭计算器"><X aria-hidden="true" /></button>
            </header>
            <div className="calculator-display" aria-live="polite">
              <small>{calculatorExpression || "Tomatotodo"}</small>
              <strong title={calculatorDisplay}>{calculatorDisplay}</strong>
            </div>
            <div className="calculator-keypad" aria-label="计算器按键">
              <button className="is-function is-clear" type="button" onClick={() => applyCalculatorAction("clear")}>AC</button>
              <button className="is-function" type="button" onClick={() => applyCalculatorAction("sign")}>±</button>
              <button className="is-function" type="button" onClick={() => applyCalculatorAction("percent")}>%</button>
              <button className="is-operator" type="button" onClick={() => applyCalculatorAction("÷")} aria-label="除">÷</button>
              {["7", "8", "9"].map((key) => <button type="button" onClick={() => applyCalculatorAction(key)} key={key}>{key}</button>)}
              <button className="is-operator" type="button" onClick={() => applyCalculatorAction("×")} aria-label="乘">×</button>
              {["4", "5", "6"].map((key) => <button type="button" onClick={() => applyCalculatorAction(key)} key={key}>{key}</button>)}
              <button className="is-operator" type="button" onClick={() => applyCalculatorAction("−")} aria-label="减">−</button>
              {["1", "2", "3"].map((key) => <button type="button" onClick={() => applyCalculatorAction(key)} key={key}>{key}</button>)}
              <button className="is-operator" type="button" onClick={() => applyCalculatorAction("+")} aria-label="加">+</button>
              <button className="is-backspace" type="button" onClick={() => applyCalculatorAction("backspace")} aria-label="退格"><Backspace aria-hidden="true" /></button>
              <button type="button" onClick={() => applyCalculatorAction("0")}>0</button>
              <button type="button" onClick={() => applyCalculatorAction(".")} aria-label="小数点">.</button>
              <button className="is-equals" type="button" onClick={() => applyCalculatorAction("=")} aria-label="等于">=</button>
            </div>
          </div>
        </aside>
      ) : null}

      {stopwatchOpen ? (
        <aside
          className={`stopwatch-shell ${activeUtilityWindow === "stopwatch" ? "is-front" : ""}`}
          role="dialog"
          aria-modal="false"
          aria-label="秒表"
          onPointerDownCapture={() => setActiveUtilityWindow("stopwatch")}
          onFocusCapture={() => setActiveUtilityWindow("stopwatch")}
          style={{ left: `${stopwatchPosition.x}px`, top: `${stopwatchPosition.y}px` }}
        >
          <div className="stopwatch-glass">
            <header
              className="stopwatch-header"
              onPointerDown={startStopwatchDrag}
              onPointerMove={moveStopwatch}
              onPointerUp={endStopwatchDrag}
              onPointerCancel={endStopwatchDrag}
            >
              <span><Timer weight="light" aria-hidden="true" /><strong>秒表</strong><small>分段计时</small></span>
              <button type="button" onClick={() => setStopwatchOpen(false)} aria-label="关闭秒表"><X aria-hidden="true" /></button>
            </header>
            <div className="stopwatch-display" aria-live="off">
              <small>分　 秒　 毫秒</small>
              <strong aria-label={`${formatStopwatchTime(stopwatchElapsedMs)}，分、秒、毫秒`}>{formatStopwatchTime(stopwatchElapsedMs)}</strong>
              <span className={stopwatchRunning ? "is-running" : ""}><i aria-hidden="true" />{stopwatchRunning ? "计时中" : stopwatchElapsedMs > 0 ? "已停止" : "准备计时"}</span>
            </div>
            <div className="stopwatch-controls" aria-label="秒表控制">
              <button className="is-primary" type="button" onClick={toggleStopwatch}>{stopwatchRunning ? <Pause weight="fill" /> : <Play weight="fill" />}<span>{stopwatchRunning ? "暂停" : stopwatchElapsedMs > 0 ? "继续" : "开始"}</span></button>
              <button type="button" onClick={addStopwatchLap} disabled={stopwatchElapsedMs < 10}><Flag weight="fill" /><span>分段</span></button>
              <button type="button" onClick={stopStopwatch} disabled={!stopwatchRunning}><Stop weight="fill" /><span>停止</span></button>
              <button className="is-danger" type="button" onClick={clearStopwatch} disabled={stopwatchElapsedMs < 10 && !stopwatchLaps.length}><Trash weight="fill" /><span>删除</span></button>
            </div>
            <div className="stopwatch-laps" aria-label="分段记录">
              <header><strong>分段记录</strong><small>{stopwatchLaps.length} 段</small></header>
              <div>
                {stopwatchLaps.map((lap, index) => (
                  <article key={lap.id}>
                    <span>#{String(stopwatchLaps.length - index).padStart(2, "0")}</span>
                    <small>本段 {formatStopwatchTime(lap.delta)}</small>
                    <strong>{formatStopwatchTime(lap.total)}</strong>
                  </article>
                ))}
                {!stopwatchLaps.length ? <p>点击“分段”记录阶段时间。</p> : null}
              </div>
            </div>
          </div>
        </aside>
      ) : null}

      {activeQuickNote ? (
        <aside
          className={`quick-note-shell ${activeUtilityWindow === "note" ? "is-front" : ""}`}
          role="dialog"
          aria-modal="false"
          aria-label="快捷便签"
          onPointerDownCapture={() => setActiveUtilityWindow("note")}
          onFocusCapture={() => setActiveUtilityWindow("note")}
          style={{ left: `${quickNotePosition.x}px`, top: `${quickNotePosition.y}px` }}
        >
          <div className="quick-note-glass">
          <div className="quick-note-card">
          <header
            className="quick-note-card-header"
            onPointerDown={startQuickNoteDrag}
            onPointerMove={moveQuickNote}
            onPointerUp={endQuickNoteDrag}
            onPointerCancel={endQuickNoteDrag}
          >
            <span><NotePencil weight="light" aria-hidden="true" />快捷便签</span>
            <button type="button" onClick={closeQuickNote} aria-label="关闭并保存便签"><X aria-hidden="true" /></button>
          </header>
          <div className="quick-note-dates">
            <span>创建 {quickNoteDate(activeQuickNote.createdAt, false)}</span>
            <span>最后编辑 {quickNoteDate(activeQuickNote.updatedAt)}</span>
          </div>
          <textarea
            autoFocus
            value={activeQuickNote.content}
            onChange={(event) => updateQuickNote(event.target.value)}
            placeholder="写下一点想法……"
            aria-label="便签内容"
            spellCheck="false"
          />
          <footer>
            <span className="quick-note-saved"><i aria-hidden="true" />已自动保存</span>
            <span className="quick-note-character-count">{Array.from(activeQuickNote.content).length} 字</span>
          </footer>
          </div>
          </div>
        </aside>
      ) : null}

      {toolsOpen ? (
        <section className="tools-workspace" aria-labelledby="tools-workspace-title">
          <div className="workspace-page-shell tools-material-shell workspace-page-enter">
            <header className="workspace-page-header tools-material-header">
              <button className="workspace-title-action" type="button" onClick={() => scrollWorkspaceToTop("tools")}><h2 id="tools-workspace-title">工具</h2></button>
            </header>

            <div className="tools-personalization-layout">
              <section className="tools-preference-block tool-notes-section" aria-labelledby="tools-notes-heading">
                <div className="tools-section-heading">
                  <h3 id="tools-notes-heading"><NotePencil weight="fill" aria-hidden="true" />快捷便笺</h3>
                  <div><small>{quickNotes.length} 条便笺 · 自动保存</small><button type="button" onClick={createQuickNote}><Plus />新建</button></div>
                </div>
                <div className="tools-note-list">
                  {sortedQuickNotes.length ? sortedQuickNotes.map((note) => {
                    const preview = note.content.trim().split(/\r?\n/)[0] || "空白便笺";
                    return (
                      <article className={note.id === openQuickNoteId ? "is-active" : ""} key={note.id}>
                        <button type="button" onClick={() => showQuickNote(note)}>
                          <strong>{preview}</strong>
                          <small>编辑于 {quickNoteDate(note.updatedAt)}</small>
                        </button>
                        <button
                          className={`tools-note-delete ${deletingQuickNoteId === note.id ? "is-holding" : ""}`}
                          type="button"
                          onPointerDown={(event) => startQuickNoteDelete(event, note.id)}
                          onPointerUp={cancelQuickNoteDelete}
                          onPointerCancel={cancelQuickNoteDelete}
                          onPointerLeave={cancelQuickNoteDelete}
                          onContextMenu={(event) => event.preventDefault()}
                          aria-label={`长按两秒删除便笺：${preview}`}
                          title="长按 2 秒删除"
                        ><Trash /><i aria-hidden="true" /></button>
                      </article>
                    );
                  }) : <p>还没有便笺，点击“新建”开始记录。</p>}
                </div>
              </section>

              <section className="tools-preference-block tools-launcher-section" aria-labelledby="tools-launcher-heading">
                <div className="tools-section-heading">
                  <h3 id="tools-launcher-heading"><Gear weight="fill" aria-hidden="true" />其他工具</h3>
                  <small>需要时打开，可自由移动窗口</small>
                </div>
                <div className="tools-launcher-list">
                  <article>
                    <div><span className="tool-card-icon"><Translate weight="fill" aria-hidden="true" /></span><span><strong>翻译</strong><small>使用必应翻译，适合阅读资料时随手查询</small></span></div>
                    <button type="button" onClick={() => {
                      setTranslatorOpen(true);
                      setActiveUtilityWindow("translator");
                    }}>打开<ArrowRight /></button>
                  </article>
                  <article>
                    <div><span className="tool-card-icon"><Calculator weight="fill" aria-hidden="true" /></span><span><strong>计算器</strong><small>完成四则运算、百分比与正负数切换</small></span></div>
                    <button type="button" onClick={() => {
                      setCalculatorOpen(true);
                      setActiveUtilityWindow("calculator");
                    }}>打开<ArrowRight /></button>
                  </article>
                  <article>
                    <div><span className="tool-card-icon"><Timer weight="fill" aria-hidden="true" /></span><span><strong>秒表</strong><small>记录分、秒与毫秒，支持阶段分段</small></span></div>
                    <button type="button" onClick={() => {
                      setStopwatchOpen(true);
                      setActiveUtilityWindow("stopwatch");
                    }}>打开<ArrowRight /></button>
                  </article>
                </div>
              </section>
            </div>
          </div>
        </section>
      ) : null}

      <section className={`dashboard-grid-workspace ${dashboardEditing ? "is-editing" : ""} ${!settingsOpen && !dashboardVisible && !toolsOpen ? "workspace-page-enter" : ""}`} aria-labelledby="dashboard-grid-title">
        <header className="dashboard-grid-header">
          <div><button className="workspace-title-action" type="button" onClick={() => scrollWorkspaceToTop("dashboard")}><h1 id="dashboard-grid-title">仪表盘</h1></button></div>
          <div className="dashboard-grid-toolbar">
            {dashboardEditing ? (
              <>
                <button type="button" onClick={() => setDashboardAddOpen(true)} aria-label="添加组件" title="添加组件"><Plus weight="bold" /></button>
                <button className="is-primary" type="button" onClick={saveDashboardGrid} aria-label="保存仪表盘布局" title="保存布局"><Check weight="bold" /></button>
              </>
            ) : (
              <button type="button" onClick={() => setDashboardEditing(true)} aria-label="编辑仪表盘" title="编辑仪表盘"><NotePencil weight="duotone" /></button>
            )}
          </div>
        </header>

        <div className="dashboard-widget-grid" aria-label="仪表盘组件">
          {dashboardGridLayout.order.map((widgetId) => {
            if (!dashboardGridLayout.visible[widgetId]) return null;
            const widget = DASHBOARD_WIDGETS[widgetId];
            if (!widget) return null;
            return (
              <article
                className={`dashboard-widget-card dashboard-widget-${widgetId} ${draggingDashboardWidget === widgetId ? "is-dragging" : ""}`}
                data-dashboard-widget={widgetId}
                style={{ "--widget-w": widget.width, "--widget-h": widget.height }}
                key={widgetId}
                onPointerDown={(event) => startDashboardWidgetLongPress(event, widgetId)}
                onPointerMove={moveDashboardWidgetLongPress}
                onPointerUp={finishDashboardWidgetLongPress}
                onPointerCancel={finishDashboardWidgetLongPress}
                title={dashboardEditing ? "长按并拖动可调整位置" : undefined}
              >
                {dashboardEditing ? (
                  <button className="dashboard-widget-remove" type="button" onClick={() => setDashboardWidgetVisibility(widgetId, false)} aria-label={`关闭组件：${widget.label}`}><X weight="bold" /></button>
                ) : null}
                {renderDashboardWidget(widgetId)}
              </article>
            );
          })}
        </div>

        {dashboardAddPresence.mounted && typeof document !== "undefined" ? createPortal((
          <div className={`dashboard-add-layer ${dashboardAddPresence.closing ? "is-closing" : ""}`}>
            <button className="dashboard-add-backdrop" type="button" onClick={() => setDashboardAddOpen(false)} aria-label="关闭添加组件面板" />
            <aside className="dashboard-add-drawer" aria-labelledby="dashboard-add-title">
              <header><div><span>WIDGET LIBRARY</span><h2 id="dashboard-add-title">添加组件</h2></div><button type="button" onClick={() => setDashboardAddOpen(false)} aria-label="关闭"><X /></button></header>
              <p>选择要恢复到仪表盘的组件。组件会按标准网格自动对齐。</p>
              <div className="dashboard-add-list">
                {dashboardGridLayout.order.filter((widgetId) => !dashboardGridLayout.visible[widgetId]).map((widgetId) => {
                  const widget = DASHBOARD_WIDGETS[widgetId];
                  return (
                    <article key={widgetId}>
                      <div><strong>{widget.label}</strong><small>{widget.detail}</small><span>{widget.width} × {widget.height}</span></div>
                      <button type="button" onClick={() => setDashboardWidgetVisibility(widgetId, true)} aria-label={`添加组件：${widget.label}`}><Plus weight="bold" /></button>
                    </article>
                  );
                })}
                {dashboardGridLayout.order.every((widgetId) => dashboardGridLayout.visible[widgetId]) ? <div className="dashboard-add-empty"><Check weight="duotone" /><strong>全部组件都在仪表盘中</strong><span>可先关闭一个组件，再从这里恢复。</span></div> : null}
              </div>
            </aside>
          </div>
        ), document.body
        ) : null}
      </section>

      {!settingsOpen && !dashboardVisible && !toolsOpen ? (
        <div className="dashboard-fixed-actions" aria-label="计时控制">
          <button className="dashboard-start-action" type="button" onClick={toggleTimer} aria-label={isRunning ? "暂停计时" : "开始计时"} title={isRunning ? "暂停" : "开始"}>
            {isRunning ? <Pause weight="fill" /> : <Play weight="fill" />}
            <span>{minuteText}:{secondText}</span>
          </button>
          <button className="dashboard-reset-action" type="button" onClick={resetTimer} aria-label="重置计时器" title="重置"><ArrowCounterClockwise weight="bold" /></button>
        </div>
      ) : null}

      <section className="task-panel legacy-dashboard-panel" aria-labelledby="tasks-heading">
        <div className="section-heading">
          <span className="eyebrow">TASK ASSIGNMENT / 任务分配</span>
          <h1 id="tasks-heading" title={activeListName}>{activeListName}</h1>
        </div>

        <div className="task-assignment-current" aria-label={`当前分配任务：${activeTask?.title || "尚未选择"}`}>
          <span><Target weight="duotone" aria-hidden="true" />当前分配</span>
          <strong>{activeTask?.title || "从清单中选择任务"}</strong>
          <small>{activeTask?.estimatedPomodoros
            ? `预计 ${activeTask.estimatedPomodoros} 个番茄钟`
            : "点击下方任务即可分配给本轮专注"}</small>
        </div>

        <div className="task-slider-shell" ref={taskSliderShellRef}>
          <button
            className="task-slider-button is-up"
            type="button"
            onClick={() => slideTasks(-1)}
            disabled={taskSlide === 0}
            aria-label="向上翻动任务"
          >
            <CaretUp weight="bold" aria-hidden="true" />
          </button>
          <div
            className="task-list"
            ref={taskListRef}
            style={{ height: `${visibleTaskCount * TASK_ROW_STEP + 8}px` }}
            onScroll={(event) => setTaskSlide(Math.min(maxTaskSlide, Math.round(event.currentTarget.scrollTop / TASK_ROW_STEP)))}
          >
            {tasks.map((task, index) => (
              <div
                key={task.id}
                className={`task-row ${task.id === activeTaskId ? "is-active" : ""} ${task.done ? "is-done" : ""} ${reorderingTaskId === task.id ? "is-reordering" : ""}`}
                data-task-reorder-id={task.id}
                title="长按并拖动可调整任务顺序"
                onPointerDown={(event) => startTaskLongPress(event, task.id)}
                onPointerMove={moveTaskLongPress}
                onPointerUp={finishTaskLongPress}
                onPointerCancel={finishTaskLongPress}
              >
              <button
                className="task-select"
                type="button"
                onClick={() => selectTask(task.id)}
                aria-label={`选择任务：${task.title}`}
              >
                <span className="task-number">{String(index + 1).padStart(2, "0")}</span>
                <span className="task-copy">
                  <span className="task-title">{task.title}</span>
                  {task.estimatedPomodoros ? (
                    <small className="task-pomodoro-goal">
                      <i className="tomato-mark" aria-hidden="true" />
                      {Math.min(completedPomodorosByTask.get(task.id) || 0, task.estimatedPomodoros)}/{task.estimatedPomodoros}
                    </small>
                  ) : null}
                </span>
              </button>
              <button
                className="task-check"
                type="button"
                onClick={() => toggleDone(task.id)}
                aria-label={task.done ? `取消完成：${task.title}` : `完成任务：${task.title}`}
              >
                {task.done ? <Check weight="bold" /> : null}
              </button>
              <button
                className="task-delete"
                type="button"
                onClick={() => removeTask(task.id)}
                aria-label={`删除任务：${task.title}`}
              >
                <Trash aria-hidden="true" />
              </button>
              </div>
            ))}
          </div>
          <button
            className="task-slider-button is-down"
            type="button"
            onClick={() => slideTasks(1)}
            disabled={taskSlide >= maxTaskSlide}
            aria-label="向下翻动任务"
          >
            <CaretDown weight="bold" aria-hidden="true" />
          </button>
          <span className="task-slider-index" aria-hidden="true">
            {tasks.length
              ? `${String(taskSlide + 1).padStart(2, "0")}–${String(Math.min(taskSlide + visibleTaskCount, tasks.length)).padStart(2, "0")}`
              : "00"} / {String(tasks.length).padStart(2, "0")}
          </span>
        </div>

        {isAdding ? (
          <form className="task-form" onSubmit={addTask}>
            <input
              autoFocus
              value={newTask}
              onChange={(event) => setNewTask(event.target.value)}
              placeholder="输入新的练习任务"
              aria-label="新的任务名称"
            />
            <label className="pomodoro-estimate-field">
              <i className="tomato-mark" aria-hidden="true" />
              <input
                type="number"
                min="1"
                max="99"
                inputMode="numeric"
                value={newTaskEstimate}
                onChange={(event) => setNewTaskEstimate(event.target.value)}
                placeholder="可选"
                aria-label="预计需要的番茄钟数量，可选"
              />
              <span>个</span>
            </label>
            <button type="submit">加入</button>
          </form>
        ) : (
          <button className="add-task" type="button" onClick={() => setIsAdding(true)}>
            <Plus aria-hidden="true" />
            新建任务
          </button>
        )}

        <div className="task-summary">
          <span>已完成</span>
          <strong>{completedTasks}</strong>
          <span>/ {tasks.length}</span>
          <div className="summary-track" aria-hidden="true">
            <span style={{ width: `${tasks.length ? (completedTasks / tasks.length) * 100 : 0}%` }} />
          </div>
        </div>
      </section>

      <section className="timer-stage legacy-dashboard-panel" aria-label="专注计时器">
        <header className="timer-dashboard-heading">
          <span>FOCUS SESSION</span>
          <strong>{countUpEnabled ? "正向记录" : mode === "focus" ? "专注计时" : "短休计时"}</strong>
          <small>{isRunning ? "本轮正在进行" : "准备好后开始"}</small>
        </header>
        <div className="red-axis" aria-hidden="true" />
        <div
          className="timer-orbit"
          style={{ "--timer-angle": `${progress * 360 - 90}deg` }}
          aria-hidden="true"
        >
          <svg className="timer-orbit-rings" viewBox="0 0 100 100" focusable="false">
            <circle className="orbit-total" cx="50" cy="50" r="48" />
            <circle
              className="orbit-progress"
              cx="50"
              cy="50"
              r="48"
              strokeDasharray={`${TIMER_ORBIT_CIRCUMFERENCE} ${TIMER_ORBIT_CIRCUMFERENCE}`}
              strokeDashoffset={TIMER_ORBIT_CIRCUMFERENCE * (1 - progress)}
            />
          </svg>
          <span className="orbit-hand"><i /></span>
        </div>

        <div className="timer-copy">
          <span className="timer-mode">Tomatotodo</span>
          <div className="time-display" aria-live="off" aria-label={`${minuteText}分${secondText}秒`}>
            <span>{minuteText}</span>
            <b>:</b>
            <span>{secondText}</span>
          </div>
        </div>

      </section>

      <aside className="info-panel legacy-dashboard-panel">
        <header className="context-dashboard-heading">
          <span>SMART WIDGETS</span>
          <strong>今日仪表盘</strong>
        </header>
        <div className="context-stack">
          {contextLayout.order.map((componentId) => {
            if (!contextLayout.visible[componentId]) return null;
            if (componentId === "date") {
              return (
                <section className="date-block context-module" aria-label="日期与当前时间" key={componentId}>
                  <div className="date-heading">
                    <div className="date-copy">
                      <span>{dateCopy.numeric}</span>
                      <strong>{dateCopy.weekday}</strong>
                    </div>
                    <div className="clock-live">
                      <small>当前时间</small>
                      <time dateTime={currentTime.toISOString()}>{clockCopy}</time>
                    </div>
                  </div>
                </section>
              );
            }
            if (componentId === "weather") return (
              <section className="weather-block context-module" aria-label="实时天气" key={componentId}>
                <div className="live-context">
                  <div
                    className={`weather-live is-${weather.status}`}
                    aria-live="polite"
                    title={weather.fallback ? "未获得定位，显示北京天气" : "已使用当前位置天气"}
                  >
                    <CloudSun weight="duotone" aria-hidden="true" />
                    <span>
                      <b><MapPin weight="fill" aria-hidden="true" />{weather.location}</b>
                      <small>{weather.description}</small>
                    </span>
                    <strong>{Number.isFinite(weather.temperature) ? `${weather.temperature}°` : "--"}</strong>
                  </div>
                </div>
              </section>
            );
            if (componentId === "countdown") return (
              <section className="countdown-block context-module" aria-label={countdownCopy} key={componentId}>
                <CalendarBlank weight="duotone" aria-hidden="true" />
                <div className="countdown-copy">
                  <small>距离 <strong>{countdownLabel}</strong> 还有</small>
                  <span><b>{countdownRemainingDays}</b> 天</span>
                </div>
              </section>
            );
            if (componentId === "calendar") return (
              <section className="mini-calendar context-module" aria-label={`${contextCalendarMonth}日历`} key={componentId}>
                <div className="mini-calendar-heading">
                  <span><CalendarBlank weight="light" aria-hidden="true" />月历</span>
                  <strong>{contextCalendarMonth}</strong>
                </div>
                <div className="mini-calendar-weekdays" aria-hidden="true">
                  {['一', '二', '三', '四', '五', '六', '日'].map((weekday) => <span key={weekday}>{weekday}</span>)}
                </div>
                <div className="mini-calendar-grid">
                  {contextCalendarDays.map((day) => {
                    const dateKey = toDateKey(day);
                    const isToday = dateKey === todayKey;
                    const isOutside = day.getMonth() !== currentTime.getMonth();
                    return (
                      <time
                        className={`${isToday ? "is-today" : ""} ${isOutside ? "is-outside" : ""}`}
                        dateTime={dateKey}
                        aria-current={isToday ? "date" : undefined}
                        aria-label={`${day.getMonth() + 1}月${day.getDate()}日${isToday ? "，今天" : ""}`}
                        key={dateKey}
                      >{day.getDate()}</time>
                    );
                  })}
                </div>
              </section>
            );
            return (
              <section className={`media-block context-module ${displayedMedia.available ? "has-media" : ""} ${mediaSource === "local" ? "is-local-media" : ""}`} aria-label={mediaSource === "local" ? "本地音乐播放器" : "Spotify 当前媒体"} key={componentId}>
                <div className="media-artwork" aria-hidden="true">
                  {displayedMedia.artwork ? <img src={displayedMedia.artwork} alt="" /> : mediaSource === "local" ? <MusicNotes weight="fill" /> : <Play weight="fill" />}
                </div>
                <div className="media-body">
                  <div className="media-details">
                    <strong className="media-title" title={displayedMedia.title}>{displayedMedia.title}</strong>
                    {displayedMedia.artist ? <small className="media-artist" title={displayedMedia.artist}>{displayedMedia.artist}</small> : null}
                  </div>
                  {mediaSource === "local" && currentLocalTrack ? (
                    <div className="local-media-player">
                    <div className="local-media-progress">
                      <input
                        type="range"
                        min="0"
                        max={Math.max(localDuration, 0)}
                        step="0.1"
                        value={Math.min(localCurrentTime, localDuration || 0)}
                        onChange={seekLocalTrack}
                        style={{ "--media-progress": `${localDuration ? (localCurrentTime / localDuration) * 100 : 0}%` }}
                        aria-label="音乐播放进度"
                      />
                      <span>{formatMediaTime(localCurrentTime)} / {formatMediaTime(localDuration)}</span>
                    </div>
                    <div className="local-media-controls">
                      <div className="local-media-transport" aria-label="本地音乐播放控制">
                        <button type="button" onClick={() => changeLocalTrack(-1)} aria-label="上一首"><SkipBack weight="fill" /></button>
                        <button className="local-media-toggle" type="button" onClick={toggleLocalPlayback} aria-label={localPlaying ? "暂停" : "播放"}>
                          {localPlaying ? <Pause weight="fill" /> : <Play weight="fill" />}
                        </button>
                        <button type="button" onClick={() => changeLocalTrack(1)} aria-label="下一首"><SkipForward weight="fill" /></button>
                        <button className="local-media-playback-mode" type="button" onClick={cycleLocalPlaybackMode} aria-label={`${localPlaybackMode === "repeat-one" ? "单曲循环" : localPlaybackMode === "shuffle" ? "随机播放" : "顺序播放"}，点击切换播放模式`}>
                          {localPlaybackMode === "shuffle" ? <Shuffle weight="bold" /> : localPlaybackMode === "repeat-one" ? <Repeat weight="bold" /> : <List weight="bold" />}
                        </button>
                      </div>
                      <label className="local-media-volume">
                        <SpeakerHigh aria-hidden="true" />
                        <input
                          type="range"
                          min="0"
                          max="1"
                          step="0.01"
                          value={localVolume}
                          onChange={changeLocalVolume}
                          style={{ "--media-progress": `${localVolume * 100}%` }}
                          aria-label="音量"
                        />
                      </label>
                    </div>
                    </div>
                  ) : null}
                </div>
              </section>
            );
          })}
        </div>

        <div className="active-task-block">
          <span>正在进行</span>
          <strong>{activeTask?.title || "选择一项任务"}</strong>
        </div>

        <div className={`session-note ${quoteMode ? "is-copyable" : ""}`}>
          <span className="note-line" />
          <p
            role={quoteMode ? "button" : undefined}
            tabIndex={quoteMode ? 0 : undefined}
            aria-label={quoteMode ? `复制名言：${remoteQuote || FOCUS_QUOTES[quoteIndex]}` : undefined}
            onClick={quoteMode ? copyCurrentQuote : undefined}
            onKeyDown={quoteMode ? (event) => {
              if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                copyCurrentQuote();
              }
            } : undefined}
          >
            {quoteMode
              ? (remoteQuote || FOCUS_QUOTES[quoteIndex])
              : isRunning ? "专注期间，先把注意力留在纸面上。" : "准备好后，开始这一段不被打断的时间。"}
          </p>
        </div>

        <div className={`mode-picker ${countUpEnabled ? "is-disabled" : ""}`} role="group" aria-label="计时模式" aria-disabled={countUpEnabled}>
          <div className="mode-segments">
            {Object.entries(MODES).map(([key, value]) => (
              <button
                key={key}
                className={`mode-option mode-${key} ${mode === key ? "is-active" : ""}`}
                type="button"
                onClick={() => chooseMode(key)}
                disabled={countUpEnabled || (key === "short" && !timerSettings.shortBreakEnabled)}
                aria-pressed={mode === key}
                aria-label={`${value.label}，${timerSettings[key]} 分钟${key === "short" && !timerSettings.shortBreakEnabled ? "，已关闭" : ""}`}
              >
                <span className="mode-label">{value.label}</span>
                <span className="mode-duration"><strong>{timerSettings[key]}</strong><small>分钟</small></span>
              </button>
            ))}
          </div>
        </div>

        <div className="timer-controls side-timer-controls">
          <GlassSurface
            width="100%"
            height="100%"
            borderRadius={24}
            backgroundOpacity={theme === "dark" ? 0.12 : 0.24}
            saturation={1.65}
            brightness={58}
            distortionScale={-120}
            className={`primary-glass side-primary-glass ${isRunning ? "is-running" : ""}`}
          >
            <button className="primary-action" type="button" onClick={toggleTimer}>
              {isRunning ? <Pause weight="fill" aria-hidden="true" /> : <Play weight="fill" aria-hidden="true" />}
            </button>
          </GlassSurface>
          <button className="reset-action" type="button" onClick={resetTimer} aria-label="重置计时器">
            <ArrowCounterClockwise aria-hidden="true" />
          </button>
        </div>
      </aside>

      {settingsOpen ? (
        <div className="settings-glass">
        <div className="settings-popover workspace-page-enter" key={settingsTab} role="dialog" aria-modal="false" aria-labelledby="settings-title">
          <div className="settings-header">
            <div>
              <button className="workspace-title-action" type="button" onClick={() => scrollWorkspaceToTop(settingsTab === "tasks" ? "config" : settingsTab)}><h2 id="settings-title">{settingsTab === "tasks" ? "配置" : settingsTab === "personalization" ? "个性化" : "常规"}</h2></button>
            </div>
            {settingsTab === "tasks" ? (
              <div className="settings-header-actions">
                <button type="button" onClick={openPresetSort} aria-label="配置排序" title="配置排序"><SortAscending weight="bold" /></button>
              </div>
            ) : null}
          </div>

          {settingsTab === "tasks" ? (
            <section className="config-presets-workspace" aria-label="任务清单预设">
              <div className="preset-card-grid">
                {taskLists.map((list) => {
                  const doneCount = list.tasks.filter((task) => task.done).length;
                  const tomatoBudget = list.tasks.reduce((sum, task) => sum + (parseTaskEstimate(task.estimatedPomodoros) || 0), 0);
                  const completion = list.tasks.length ? (doneCount / list.tasks.length) * 100 : 0;
                  return (
                    <article className={`preset-summary-card ${list.id === activeListId ? "is-active" : ""}`} key={list.id}>
                      <header>
                        <button type="button" onClick={() => switchTaskList(list)}>
                          <span>{list.id === activeListId ? "当前清单" : "任务预设"}</span>
                          <strong>{list.name}</strong>
                        </button>
                        <button className="preset-card-menu-button" type="button" onClick={() => setPresetCardMenuId((current) => current === list.id ? null : list.id)} aria-label={`打开${list.name}菜单`} aria-expanded={presetCardMenuId === list.id}><DotsThreeVertical weight="bold" /></button>
                        {presetCardMenuId === list.id ? (
                          <div className="preset-card-menu" role="menu">
                            <button type="button" role="menuitem" onClick={() => { switchTaskList(list); setPresetCardMenuId(null); }}><Check />设为当前</button>
                            <button type="button" role="menuitem" onClick={() => openEditPreset(list)}><PencilSimple />编辑</button>
                            <button className="is-danger" type="button" role="menuitem" onClick={() => deleteTaskListById(list.id)} disabled={taskLists.length <= 1}><Trash />删除</button>
                          </div>
                        ) : null}
                      </header>
                      <div className="preset-summary-progress" aria-label={`完成 ${doneCount}/${list.tasks.length}`}><span style={{ width: `${completion}%` }} /></div>
                      <div className="preset-summary-tasks">
                        {list.tasks.slice(0, 4).map((task, index) => (
                          <div className={task.done ? "is-done" : ""} key={task.id}>
                            <span>{String(index + 1).padStart(2, "0")}</span>
                            <p><strong>{task.title}</strong>{task.subtitle ? <small>{task.subtitle}</small> : null}</p>
                            {task.estimatedPomodoros ? <b>{task.estimatedPomodoros}<i className="tomato-mark" aria-hidden="true" /></b> : null}
                          </div>
                        ))}
                        {!list.tasks.length ? <p className="preset-summary-empty">还没有任务，点击菜单开始编辑。</p> : null}
                      </div>
                      <footer><span>{doneCount}/{list.tasks.length} 已完成</span><span>{tomatoBudget ? `预计 ${tomatoBudget} 个番茄` : "未设置番茄预算"}</span></footer>
                    </article>
                  );
                })}
              </div>

              {presetDrawerPresence.mounted && typeof document !== "undefined" ? createPortal((
                <div className={`preset-builder-layer ${presetDrawerPresence.closing ? "is-closing" : ""}`}>
                  <button className="preset-builder-backdrop" type="button" onClick={() => setPresetDrawerOpen(false)} aria-label="关闭清单编辑" />
                  <aside className="preset-builder-drawer" aria-labelledby="preset-builder-title">
                    <header>
                      <div><span>LIST BUILDER</span><h3 id="preset-builder-title">{editingPresetId ? "编辑清单" : "添加预设"}</h3></div>
                      <button type="button" onClick={() => setPresetDrawerOpen(false)} aria-label="关闭"><X /></button>
                    </header>

                    <label className="preset-builder-name">
                      <span>清单名称</span>
                      <input autoFocus value={presetBuilder.name} onChange={(event) => setPresetBuilder((current) => ({ ...current, name: event.target.value }))} maxLength="28" placeholder="" />
                    </label>

                    <div className="preset-builder-section-heading"><span>任务内容</span><small>{presetBuilder.tasks.length} 项 · 可调整顺序</small></div>

                    <div className="preset-builder-task-list">
                      {presetBuilder.tasks.map((task, index) => (
                        <article
                          className={draggedBuilderTaskId === task.id ? "is-dragging" : ""}
                          key={task.id}
                          onDragOver={(event) => event.preventDefault()}
                          onDrop={(event) => {
                            event.preventDefault();
                            reorderPresetBuilderTasks(draggedBuilderTaskId, task.id);
                            setDraggedBuilderTaskId(null);
                          }}
                        >
                          <span className="preset-builder-index">{String(index + 1).padStart(2, "0")}</span>
                          <div className="preset-builder-task-fields">
                            <input value={task.title} onChange={(event) => updatePresetBuilderTask(task.id, { title: event.target.value })} placeholder="任务名称" aria-label={`任务 ${index + 1} 名称`} />
                            <input value={task.subtitle || ""} onChange={(event) => updatePresetBuilderTask(task.id, { subtitle: event.target.value })} placeholder="小标题（可选）" aria-label={`任务 ${index + 1} 小标题`} />
                          </div>
                          <label className="preset-builder-estimate"><input type="number" min="1" max="99" value={task.estimatedPomodoros || ""} onChange={(event) => updatePresetBuilderTask(task.id, { estimatedPomodoros: clampTaskEstimateInput(event.target.value) })} aria-label={`任务 ${index + 1} 预计番茄数`} /><span>个番茄</span></label>
                          <span
                            className="preset-builder-drag-handle"
                            draggable
                            onDragStart={(event) => {
                              event.dataTransfer.effectAllowed = "move";
                              setDraggedBuilderTaskId(task.id);
                            }}
                            onDragEnd={() => setDraggedBuilderTaskId(null)}
                            role="button"
                            tabIndex="0"
                            aria-label={`拖动排序：${task.title}`}
                            title="拖动调整顺序"
                          ><DotsSixVertical weight="bold" /></span>
                          <button className="preset-builder-delete" type="button" onClick={() => setPresetBuilder((current) => ({ ...current, tasks: current.tasks.filter((item) => item.id !== task.id) }))} aria-label="删除任务"><Trash /></button>
                        </article>
                      ))}
                      {!presetBuilder.tasks.length ? <div className="preset-builder-empty"><ListChecks weight="duotone" /><strong>从第一项任务开始</strong><span>任务会按这里的顺序出现在主页。</span></div> : null}
                    </div>

                    <form className="preset-builder-add-task" onSubmit={addPresetBuilderTask}>
                      <div>
                        <input value={presetBuilderTask.title} onChange={(event) => setPresetBuilderTask((current) => ({ ...current, title: event.target.value }))} placeholder="任务名称" aria-label="新任务名称" />
                        <input value={presetBuilderTask.subtitle} onChange={(event) => setPresetBuilderTask((current) => ({ ...current, subtitle: event.target.value }))} placeholder="小标题（可选）" aria-label="新任务小标题" />
                      </div>
                      <label><input type="number" min="1" max="99" value={presetBuilderTask.estimatedPomodoros} onChange={(event) => setPresetBuilderTask((current) => ({ ...current, estimatedPomodoros: clampTaskEstimateInput(event.target.value) }))} placeholder="可选" aria-label="预计番茄数量" /><span>个番茄</span></label>
                      <button type="submit" aria-label="添加任务" title="添加任务"><Plus weight="bold" aria-hidden="true" /></button>
                    </form>

                    <footer>
                      <span>{presetBuilder.tasks.length ? `共 ${presetBuilder.tasks.length} 项任务` : "可以先保存空白清单"}</span>
                      <button type="button" onClick={savePresetBuilder}><FloppyDisk weight="fill" />保存</button>
                    </footer>
                  </aside>
                </div>
              ), document.querySelector(".app-shell") || document.body
              ) : null}

              {presetSortPresence.mounted && typeof document !== "undefined" ? createPortal((
                <div className={`preset-sort-layer ${presetSortPresence.closing ? "is-closing" : ""}`}>
                  <button className="preset-sort-backdrop" type="button" onClick={() => setPresetSortOpen(false)} aria-label="关闭配置排序" />
                  <aside className="preset-sort-drawer" aria-labelledby="preset-sort-title">
                    <header>
                      <button type="button" onClick={() => setPresetSortOpen(false)} aria-label="返回配置"><ArrowLeft /></button>
                      <h3 id="preset-sort-title">配置排序</h3>
                      <button className="preset-sort-save" type="button" onClick={savePresetSort} aria-label="保存清单顺序"><Check weight="bold" /></button>
                    </header>
                    <div className="preset-sort-list">
                      {presetSortDraft.map((listId) => {
                        const list = taskLists.find((item) => item.id === listId);
                        if (!list) return null;
                        return (
                          <article
                            className={draggedPresetId === listId ? "is-dragging" : ""}
                            key={listId}
                            draggable
                            onDragStart={() => setDraggedPresetId(listId)}
                            onDragEnd={() => setDraggedPresetId(null)}
                            onDragOver={(event) => event.preventDefault()}
                            onDrop={(event) => {
                              event.preventDefault();
                              reorderPresetSortItems(draggedPresetId, listId);
                              setDraggedPresetId(null);
                            }}
                          >
                            <strong>{list.name}</strong>
                            <small>{list.tasks.length} 项任务{list.id === activeListId ? " · 当前清单" : ""}</small>
                            <div className="preset-sort-controls">
                              <span aria-hidden="true"><DotsSixVertical weight="bold" /></span>
                            </div>
                          </article>
                        );
                      })}
                    </div>
                    <p>拖动清单卡片调整顺序。</p>
                  </aside>
                </div>
              ), document.querySelector(".app-shell") || document.body
              ) : null}
            </section>
          ) : null}

          {settingsTab === "personalization" ? (
          <div className="personalization-workspace">
            <section className="personalization-block" aria-labelledby="appearance-heading">
              <h3 id="appearance-heading"><SlidersHorizontal weight="fill" aria-hidden="true" />主题模式</h3>
              <div className="personalization-theme-options" role="group" aria-label="主题模式">
                <button className={appearanceMode === "auto" ? "is-active" : ""} type="button" onClick={() => selectAppearanceMode("auto")}>
                  <ArrowCounterClockwise weight="bold" aria-hidden="true" />
                  <span><strong>自动</strong><small>{sunTimeCopy}</small></span>
                </button>
                <button className={appearanceMode === "light" ? "is-active" : ""} type="button" onClick={() => selectAppearanceMode("light")}>
                  <Sun weight="fill" aria-hidden="true" />
                  <span><strong>浅色</strong><small>明亮柔和界面</small></span>
                </button>
                <button className={appearanceMode === "dark" ? "is-active" : ""} type="button" onClick={() => selectAppearanceMode("dark")}>
                  <Moon weight="fill" aria-hidden="true" />
                  <span><strong>深色</strong><small>低亮度专注界面</small></span>
                </button>
              </div>
            </section>

            <section className="personalization-block theme-color-block" aria-labelledby="palette-heading">
              <div className="personalization-heading-row">
                <h3 id="palette-heading"><Palette weight="fill" aria-hidden="true" />主题色彩</h3>
                <div>
                  <button className="scheme-selector" type="button" onClick={() => setThemeSchemeOpen(true)}>{activeThemeVariant.label}</button>
                  <button className="theme-reset" type="button" onClick={() => { setThemeVariant("tonalSpot"); setPaletteId("forest"); }} aria-label="恢复默认主题色"><ArrowCounterClockwise weight="bold" /></button>
                </div>
              </div>
              <div className="material-preset-row" role="radiogroup" aria-label="Material You 主题色预设">
                {MONET_PRESETS.map((preset) => {
                  const previewColors = materialPreviewColors(preset.seed, themeVariant);
                  const selected = paletteId === preset.id || activePalette.seed.toUpperCase() === preset.seed;
                  return (
                    <button
                      key={preset.id}
                      className={`material-preset ${selected ? "is-active" : ""}`}
                      type="button"
                      role="radio"
                      aria-checked={selected}
                      aria-label={`${preset.name}主题色`}
                      onClick={() => setPaletteId(preset.id)}
                    >
                      <span className="material-preset-disc" aria-hidden="true">
                        {previewColors.map((color, index) => <i key={`${preset.id}-${index}`} style={{ backgroundColor: color }} />)}
                        {selected ? <b><Check weight="bold" /></b> : null}
                      </span>
                    </button>
                  );
                })}
                <button
                  className={`material-preset material-custom-preset ${activePalette.id === "custom" ? "is-active" : ""}`}
                  type="button"
                  aria-label="添加自定义主题色"
                  onClick={() => { setCustomColorDraft(activePalette.seed); setCustomColorOpen(true); }}
                ><Plus weight="bold" /></button>
              </div>
              <p className="material-color-note">由 Material Color Utilities 根据种子色、明暗模式和配色方案生成完整色彩角色。</p>
            </section>

            <section className="personalization-row" aria-labelledby="pure-black-title">
              <div><span className="personalization-row-icon" aria-hidden="true"><CircleHalfTilt weight="fill" /></span><span><strong id="pure-black-title">纯黑模式</strong><small>仅在深色模式下使用纯黑背景</small></span></div>
              <label className="md3-inline-switch"><input type="checkbox" checked={pureBlack} onChange={(event) => setPureBlack(event.target.checked)} /><i aria-hidden="true" /></label>
            </section>

            <section className="personalization-row font-scale-row" aria-labelledby="font-scale-title">
              <div><span className="personalization-row-icon text-scale-icon" aria-hidden="true"><TextT weight="bold" /></span><span><strong id="font-scale-title">字体缩放</strong><small>同步调整界面文字与控件密度</small></span></div>
              <label className="md3-inline-switch"><input type="checkbox" checked={fontScaleEnabled} onChange={(event) => setFontScaleEnabled(event.target.checked)} /><i aria-hidden="true" /></label>
              <div className="font-scale-control">
                <input type="range" min="85" max="125" step="5" value={fontScale} disabled={!fontScaleEnabled} onChange={(event) => setFontScale(Number(event.target.value))} aria-label="字体缩放比例" />
                <output>{fontScaleEnabled ? fontScale : 100}%</output>
              </div>
            </section>

            {themeSchemeOpen ? (
              <div className="personalization-dialog-layer" role="presentation">
                <button type="button" className="personalization-dialog-backdrop" onClick={() => setThemeSchemeOpen(false)} aria-label="关闭配色方案" />
                <section className="theme-scheme-dialog" role="dialog" aria-modal="true" aria-labelledby="theme-scheme-title">
                  <header><h3 id="theme-scheme-title">配色方案</h3><button type="button" onClick={() => setThemeSchemeOpen(false)} aria-label="关闭"><X /></button></header>
                  <div role="radiogroup" aria-label="Material You 配色方案">
                    {MATERIAL_SCHEMES.map((scheme) => (
                      <label key={scheme.id}>
                        <input type="radio" name="theme-scheme" value={scheme.id} checked={themeVariant === scheme.id} onChange={() => { setThemeVariant(scheme.id); setThemeSchemeOpen(false); }} />
                        <i aria-hidden="true" />
                        <span>{scheme.label}</span>
                      </label>
                    ))}
                  </div>
                </section>
              </div>
            ) : null}

            {customColorOpen ? (
              <div className="personalization-dialog-layer" role="presentation">
                <button type="button" className="personalization-dialog-backdrop" onClick={() => setCustomColorOpen(false)} aria-label="关闭调色板" />
                <section className="custom-color-dialog" role="dialog" aria-modal="true" aria-labelledby="custom-color-title">
                  <header><h3 id="custom-color-title">调色板</h3><button type="button" onClick={() => setCustomColorOpen(false)} aria-label="关闭"><X /></button></header>
                  <label className="custom-color-wheel" style={{ "--selected-color": customColorDraft }}>
                    <input type="color" value={customColorDraft} onInput={(event) => setCustomColorDraft(event.currentTarget.value.toUpperCase())} onChange={(event) => setCustomColorDraft(event.target.value.toUpperCase())} aria-label="选择种子色" />
                    <span aria-hidden="true" />
                  </label>
                  <input className="custom-color-hex" value={customColorDraft} onChange={(event) => /^#[0-9a-f]{0,6}$/i.test(event.target.value) && setCustomColorDraft(event.target.value.toUpperCase())} aria-label="十六进制颜色" />
                  <footer><button type="button" onClick={() => setCustomColorOpen(false)}>取消</button><button type="button" onClick={() => { if (/^#[0-9A-F]{6}$/.test(customColorDraft)) setPaletteId(customColorDraft); setCustomColorOpen(false); }}>确定</button></footer>
                </section>
              </div>
            ) : null}
          </div>
          ) : null}

          {settingsTab === "components" ? (
          <section className="settings-section" aria-labelledby="context-layout-heading">
            <div className="context-layout-heading">
              <span id="context-layout-heading">右上角组件</span>
              <small>拖动排序，也可单独关闭</small>
            </div>
            <div
              className="context-layout-editor"
              onPointerMove={(event) => {
                if (!draggedContext) return;
                const target = document
                  .elementFromPoint(event.clientX, event.clientY)
                  ?.closest?.("[data-context-id]")
                  ?.getAttribute("data-context-id");
                if (target) reorderContextComponents(draggedContext, target);
              }}
              onPointerUp={() => setDraggedContext(null)}
              onPointerCancel={() => setDraggedContext(null)}
            >
              {contextLayout.order.map((componentId, index) => {
                const component = CONTEXT_COMPONENTS[componentId];
                const visible = contextLayout.visible[componentId];
                const hasInlineSettings = componentId === "countdown" || componentId === "media";
                const inlineSettingsOpen = openContextSettings === componentId;
                return (
                  <div className={`context-layout-item ${inlineSettingsOpen ? "is-open" : ""}`} key={componentId}>
                    <article
                      className={`context-layout-row ${draggedContext === componentId ? "is-dragging" : ""}`}
                      data-context-id={componentId}
                      draggable
                      onDragStart={(event) => {
                        setDraggedContext(componentId);
                        event.dataTransfer.effectAllowed = "move";
                        event.dataTransfer.setData("text/plain", componentId);
                      }}
                      onDragOver={(event) => {
                        event.preventDefault();
                        event.dataTransfer.dropEffect = "move";
                      }}
                      onDrop={(event) => {
                        event.preventDefault();
                        reorderContextComponents(event.dataTransfer.getData("text/plain") || draggedContext, componentId);
                        setDraggedContext(null);
                      }}
                      onDragEnd={() => setDraggedContext(null)}
                    >
                      <button
                        className="context-drag-handle"
                        type="button"
                        onPointerDown={(event) => {
                          event.preventDefault();
                          setDraggedContext(componentId);
                          event.currentTarget.setPointerCapture?.(event.pointerId);
                        }}
                        aria-label={`拖动${component.label}调整顺序`}
                      >⠿</button>
                      <span className="context-layout-copy">
                        <strong>{component.label}</strong>
                        <small>{component.detail}</small>
                      </span>
                      {hasInlineSettings ? (
                        <button
                          className={`context-config-toggle ${inlineSettingsOpen ? "is-open" : ""}`}
                          type="button"
                          onClick={() => setOpenContextSettings((current) => current === componentId ? null : componentId)}
                          aria-expanded={inlineSettingsOpen}
                          aria-controls={`${componentId}-inline-settings`}
                          aria-label={`${inlineSettingsOpen ? "收起" : "展开"}${component.label}设置`}
                        ><CaretRight /></button>
                      ) : <span className="context-config-spacer" aria-hidden="true" />}
                      <span className="context-order-buttons">
                        <button
                          type="button"
                          onClick={() => nudgeContextComponent(componentId, -1)}
                          disabled={index === 0}
                          aria-label={`上移${component.label}`}
                        ><CaretUp /></button>
                        <button
                          type="button"
                          onClick={() => nudgeContextComponent(componentId, 1)}
                          disabled={index === contextLayout.order.length - 1}
                          aria-label={`下移${component.label}`}
                        ><CaretDown /></button>
                      </span>
                      <button
                        className={`context-visibility ${visible ? "is-visible" : ""}`}
                        type="button"
                        onClick={() => toggleContextVisibility(componentId)}
                        aria-pressed={visible}
                        aria-label={`${visible ? "关闭" : "显示"}${component.label}`}
                      >{visible ? "显示" : "关闭"}</button>
                    </article>

                    {inlineSettingsOpen && componentId === "countdown" ? (
                      <div className="context-inline-settings countdown-settings" id="countdown-inline-settings">
                        <div className="countdown-settings-heading">
                          <span>
                            <strong>倒数日内容</strong>
                            <small>显示为“距离 {countdownLabel} 还有 {countdownRemainingDays} 天”</small>
                          </span>
                        </div>
                        <label className="countdown-field">
                          <span>目标名称</span>
                          <input
                            type="text"
                            value={countdown.label}
                            maxLength="24"
                            onChange={(event) => setCountdown((current) => ({ ...current, label: event.target.value }))}
                            placeholder="例如：央美校考"
                            autoComplete="off"
                          />
                        </label>
                        <label className="countdown-field">
                          <span>目标日期</span>
                          <input
                            type="date"
                            value={countdown.date}
                            min={localDateInputValue(currentTime)}
                            onChange={(event) => {
                              if (!event.target.value) return;
                              setCountdown((current) => ({ ...current, date: event.target.value }));
                            }}
                          />
                        </label>
                      </div>
                    ) : null}

                    {inlineSettingsOpen && componentId === "media" ? (
                      <div className="context-inline-settings media-settings" id="media-inline-settings">
                        <div className="media-source-picker" role="radiogroup" aria-label="媒体来源">
                          <button
                            type="button"
                            className={mediaSource === "spotify" ? "is-active" : ""}
                            role="radio"
                            aria-checked={mediaSource === "spotify"}
                            onClick={() => setMediaSource("spotify")}
                          ><span><Play weight="fill" /></span><b>Spotify</b><small>同步当前播放</small></button>
                          <button
                            type="button"
                            className={mediaSource === "local" ? "is-active" : ""}
                            role="radio"
                            aria-checked={mediaSource === "local"}
                            onClick={() => setMediaSource("local")}
                          ><span><FolderOpen weight="fill" /></span><b>本地音乐</b><small>导入音乐文件夹</small></button>
                        </div>

                        {mediaSource === "spotify" ? (
                          <div className="spotify-settings">
                            <div className="spotify-settings-heading">
                              <span>
                                <strong>Spotify 当前播放</strong>
                                <small>{spotifyStatus === "connected" ? "已连接，自动同步当前歌曲" : "读取专辑封面、音乐名与歌手"}</small>
                              </span>
                              <i className={spotifyStatus === "connected" ? "is-connected" : ""} aria-hidden="true" />
                            </div>
                            <label className="spotify-client-field">
                              <span>CLIENT ID</span>
                              <input
                                type="text"
                                value={spotifyClientId}
                                onChange={(event) => setSpotifyClientId(event.target.value)}
                                placeholder="粘贴 Spotify Client ID"
                                autoComplete="off"
                                spellCheck="false"
                              />
                            </label>
                            <div className="spotify-actions">
                              <button type="button" onClick={connectSpotify} disabled={spotifyStatus === "connecting"}>
                                {spotifyStatus === "connecting" ? "连接中…" : spotifyAuth ? "重新授权" : "连接 Spotify"}
                              </button>
                              {spotifyAuth ? <button type="button" className="spotify-disconnect" onClick={disconnectSpotify}>断开</button> : null}
                            </div>
                            <div className="spotify-redirect-note">
                              <span>在 Spotify Developer Dashboard 添加回调地址</span>
                              <code title={spotifyRedirect}>{spotifyRedirect}</code>
                              {spotifyNeedsLoopback ? <small>连接时会自动切换至 127.0.0.1；Spotify 不接受 localhost。</small> : null}
                            </div>
                            <a className="spotify-dashboard-link" href="https://developer.spotify.com/dashboard" target="_blank" rel="noreferrer">打开 Spotify Developer Dashboard ↗</a>
                          </div>
                        ) : (
                          <div className="local-media-settings">
                            <div className="local-media-settings-copy">
                              <span><MusicNotes weight="fill" /></span>
                              <div>
                                <strong>{localFolderName || "选择音乐文件夹"}</strong>
                                <small>{localTracks.length
                                  ? `${localTracks.length} 首音乐 · 支持封面与 ID3 信息`
                                  : localFolderAccess === "restoring"
                                    ? "正在恢复上次的音乐库…"
                                    : localFolderAccess === "permission"
                                      ? "文件夹已保存，确认权限后即可继续播放"
                                      : "支持 MP3、M4A、WAV、OGG、FLAC 与 Opus"}</small>
                              </div>
                            </div>
                            <div className="local-folder-actions">
                              <button
                                className="local-folder-button"
                                type="button"
                                onClick={localFolderAccess === "permission" && !localTracks.length
                                  ? () => restoreSavedLocalMusicFolder()
                                  : importLocalMusicFolder}
                                disabled={localImporting}
                              >
                                <FolderOpen weight="fill" />
                                {localImporting
                                  ? "正在读取…"
                                  : localTracks.length
                                    ? "更换文件夹"
                                    : localFolderAccess === "permission"
                                      ? "恢复读取"
                                      : "导入文件夹"}
                              </button>
                              {localFolderAccess === "permission" && !localTracks.length ? (
                                <button className="local-folder-change" type="button" onClick={importLocalMusicFolder}>更换文件夹</button>
                              ) : null}
                            </div>
                            <input
                              ref={localFolderInputRef}
                              className="local-folder-input"
                              type="file"
                              accept="audio/*,.mp3,.m4a,.aac,.wav,.ogg,.oga,.flac,.opus"
                              webkitdirectory=""
                              directory=""
                              multiple
                              onChange={handleLocalFolderFiles}
                            />
                            <p>音乐仅在本机读取，不会上传。桌面版会保存文件夹授权；浏览器要求确认时可直接恢复读取，无需重新导入。</p>
                          </div>
                        )}
                      </div>
                    ) : null}
                  </div>
                );
              })}
            </div>
            <div className="settings-other-heading">
              <span>其他</span>
              <small>提示内容与快捷入口</small>
            </div>
            <label className="settings-immersive settings-quote-mode">
              <span>
                <strong>名言警句</strong>
                <small>成长、坚持、励志与专注短句，每 5 分钟更新</small>
              </span>
              <input
                type="checkbox"
                checked={quoteMode}
                onChange={(event) => {
                  const enabled = event.target.checked;
                  setQuoteMode(enabled);
                  if (!enabled) setRemoteQuote("");
                }}
              />
              <i aria-hidden="true" />
            </label>
            <label className="settings-immersive settings-quote-mode settings-note-launcher">
              <span>
                <strong>便笺</strong>
                <small>在工具页显示快捷便笺，关闭不会删除已有内容</small>
              </span>
              <input
                type="checkbox"
                checked={quickNoteLauncherVisible}
                onChange={(event) => setQuickNoteLauncherVisible(event.target.checked)}
              />
              <i aria-hidden="true" />
            </label>
            <label className="settings-immersive settings-quote-mode settings-note-launcher">
              <span>
                <strong>翻译</strong>
                <small>在工具页显示可移动的必应翻译窗口入口</small>
              </span>
              <input
                type="checkbox"
                checked={translatorLauncherVisible}
                onChange={(event) => setTranslatorLauncherVisible(event.target.checked)}
              />
              <i aria-hidden="true" />
            </label>
            <label className="settings-immersive settings-quote-mode settings-note-launcher">
              <span>
                <strong>计算器</strong>
                <small>在工具页显示可移动的基础计算器入口</small>
              </span>
              <input
                type="checkbox"
                checked={calculatorLauncherVisible}
                onChange={(event) => setCalculatorLauncherVisible(event.target.checked)}
              />
              <i aria-hidden="true" />
            </label>
          </section>
          ) : null}

          {settingsTab === "general" ? (
          <div className="settings-general general-workspace">
            <section className="settings-section settings-general-card general-preference-section general-window-card" aria-labelledby="general-window-title">
              <div className="settings-card-heading">
                <span><CircleHalfTilt weight="fill" aria-hidden="true" /><strong id="general-window-title">模式</strong></span>
                <small>专注时的窗口显示方式</small>
              </div>
              <label className="break-toggle general-window-toggle">
                <span><strong>沉浸模式</strong><small>全屏翻页时钟，顶部显示当前专注状态</small></span>
                <input type="checkbox" checked={immersive} onChange={(event) => setImmersive(event.target.checked)} />
                <i aria-hidden="true" />
              </label>
              <label className="break-toggle general-window-toggle">
                <span><strong>小窗模式</strong><small>在系统置顶小窗显示状态、倒计时与暂停控制</small></span>
                <input type="checkbox" checked={miniWindowMode} onChange={(event) => toggleMiniWindowMode(event.target.checked)} />
                <i aria-hidden="true" />
              </label>
            </section>

            <section className="settings-section settings-general-card general-preference-section general-timer-card" aria-labelledby="settings-timer-title">
              <div className="settings-card-heading">
                <span><Clock weight="fill" aria-hidden="true" /><strong id="settings-timer-title">计时节奏</strong></span>
                <small>1–180 分钟</small>
              </div>
              <div className="duration-fields">
                <label>
                  <span>专注时长</span>
                  <div><input type="number" min="1" max="180" value={timerSettings.focus} onChange={(event) => updateTimerSetting("focus", event.target.value)} /><small>分钟</small></div>
                </label>
                <label>
                  <span>短休时长</span>
                  <div><input type="number" min="1" max="180" value={timerSettings.short} onChange={(event) => updateTimerSetting("short", event.target.value)} /><small>分钟</small></div>
                </label>
              </div>
              <label className="break-toggle">
                <span><strong>启用短休</strong><small>关闭后，右侧不再显示短休模式</small></span>
                <input
                  type="checkbox"
                  checked={timerSettings.shortBreakEnabled}
                  onChange={(event) => {
                    const enabled = event.target.checked;
                    setTimerSettings((current) => ({ ...current, shortBreakEnabled: enabled }));
                    if (!enabled && mode === "short") chooseMode("focus");
                  }}
                />
                <i aria-hidden="true" />
              </label>
              <label className="break-toggle count-up-toggle">
                <span>
                  <strong>正向计时</strong>
                  <small>适合碎片时间记录，随用随记，并计入专注日志</small>
                </span>
                <input
                  type="checkbox"
                  checked={countUpEnabled}
                  onChange={(event) => toggleCountUpMode(event.target.checked)}
                />
                <i aria-hidden="true" />
              </label>
            </section>

            <section className="settings-section settings-general-card general-preference-section general-clock-card" aria-labelledby="general-clock-title">
              <div className="settings-card-heading">
                <span><Clock weight="fill" aria-hidden="true" /><strong id="general-clock-title">圆表设置</strong></span>
                <small>控制仪表盘圆表的显示细节</small>
              </div>
              <label className="break-toggle general-window-toggle">
                <span><strong>显示刻度</strong><small>显示十二个小时刻度，便于快速读取时间</small></span>
                <input type="checkbox" checked={analogClockSettings.showMarkers} onChange={(event) => setAnalogClockSettings((current) => ({ ...current, showMarkers: event.target.checked }))} />
                <i aria-hidden="true" />
              </label>
              <label className="break-toggle general-window-toggle">
                <span><strong>显示秒针</strong><small>关闭后仅保留时针与分针，画面更加安静</small></span>
                <input type="checkbox" checked={analogClockSettings.showSeconds} onChange={(event) => setAnalogClockSettings((current) => ({ ...current, showSeconds: event.target.checked }))} />
                <i aria-hidden="true" />
              </label>
            </section>

            <section className="settings-section settings-general-card general-preference-section general-countdown-card" aria-labelledby="general-countdown-title">
              <div className="settings-card-heading">
                <span><CalendarBlank weight="fill" aria-hidden="true" /><strong id="general-countdown-title">倒数日设置</strong></span>
                <small>距离 {countdownLabel} 还有 {countdownRemainingDays} 天</small>
              </div>
              <div className="general-inline-fields">
                <label className="countdown-field">
                  <span>目标名称</span>
                  <input type="text" value={countdown.label} maxLength="24" onChange={(event) => setCountdown((current) => ({ ...current, label: event.target.value }))} placeholder="例如：央美校考" autoComplete="off" />
                </label>
                <label className="countdown-field">
                  <span>目标日期</span>
                  <input type="date" value={countdown.date} min={localDateInputValue(currentTime)} onChange={(event) => event.target.value && setCountdown((current) => ({ ...current, date: event.target.value }))} />
                </label>
              </div>
            </section>

            <section className="settings-section settings-general-card general-preference-section general-media-card" aria-labelledby="general-media-title">
              <div className="settings-card-heading">
                <span><MusicNotes weight="fill" aria-hidden="true" /><strong id="general-media-title">音乐设置</strong></span>
                <small>{mediaSource === "spotify" ? "Spotify" : "本地音乐"}</small>
              </div>
              <div className="general-media-content">
                <div className="media-source-picker general-media-source-picker" role="radiogroup" aria-label="媒体来源">
                  <button type="button" className={mediaSource === "spotify" ? "is-active" : ""} role="radio" aria-checked={mediaSource === "spotify"} onClick={() => setMediaSource("spotify")}>
                    <span><Play weight="fill" /></span>
                    <b>Spotify</b>
                    <small>同步当前播放</small>
                    <ArrowRight aria-hidden="true" />
                  </button>
                  <button type="button" className={mediaSource === "local" ? "is-active" : ""} role="radio" aria-checked={mediaSource === "local"} onClick={() => setMediaSource("local")}>
                    <span><FolderOpen weight="fill" /></span>
                    <b>本地音乐</b>
                    <small>读取设备中的音乐文件</small>
                    <ArrowRight aria-hidden="true" />
                  </button>
                </div>

                {mediaSource === "spotify" ? (
                  <div className="general-media-detail general-spotify-detail">
                    <header className="general-media-detail-heading">
                      <div><span className="general-media-detail-icon"><Play weight="fill" /></span><span><strong>Spotify</strong><small>专辑封面、歌曲名与歌手会显示在仪表盘</small></span></div>
                      <span className={`general-media-status ${spotifyStatus === "connected" ? "is-connected" : ""}`}><i aria-hidden="true" />{spotifyStatus === "connected" ? "已连接" : spotifyStatus === "connecting" ? "连接中" : "未连接"}</span>
                    </header>
                    <label className="general-media-field"><span>CLIENT ID</span><input type="text" value={spotifyClientId} onChange={(event) => setSpotifyClientId(event.target.value)} placeholder="粘贴 Spotify Client ID" autoComplete="off" spellCheck="false" /></label>
                    <div className="general-media-actions">
                      <button type="button" onClick={connectSpotify} disabled={spotifyStatus === "connecting"}>{spotifyStatus === "connecting" ? "连接中…" : spotifyAuth ? "重新授权" : "连接 Spotify"}<ArrowRight /></button>
                      {spotifyAuth ? <button type="button" className="is-secondary" onClick={disconnectSpotify}>断开连接</button> : null}
                    </div>
                    <details className="general-media-advanced">
                      <summary><span>回调地址</span><CaretRight aria-hidden="true" /></summary>
                      <code title={spotifyRedirect}>{spotifyRedirect}</code>
                      {spotifyNeedsLoopback ? <small>连接时会自动切换至 127.0.0.1；Spotify 不接受 localhost。</small> : null}
                    </details>
                  </div>
                ) : (
                  <div className="general-media-detail general-local-detail">
                    <header className="general-media-detail-heading">
                      <div><span className="general-media-detail-icon"><FolderOpen weight="fill" /></span><span><strong>{localFolderName || "本地音乐"}</strong><small>音乐只在本机读取，不会上传</small></span></div>
                      <span className={`general-media-status ${localTracks.length ? "is-connected" : ""}`}><i aria-hidden="true" />{localTracks.length ? `${localTracks.length} 首` : localFolderAccess === "permission" ? "待授权" : "未导入"}</span>
                    </header>
                    <div className="general-local-library-copy"><strong>{localTracks.length ? "音乐库已准备好" : "导入一个音乐文件夹"}</strong><small>{localTracks.length ? "支持封面、歌手和 ID3 信息" : localFolderAccess === "restoring" ? "正在恢复上次的音乐库…" : localFolderAccess === "permission" ? "文件夹已保存，确认权限后即可继续播放" : "支持 MP3、M4A、WAV、OGG、FLAC 与 Opus"}</small></div>
                    <div className="general-media-actions">
                      <button className="is-primary" type="button" onClick={localFolderAccess === "permission" && !localTracks.length ? () => restoreSavedLocalMusicFolder() : importLocalMusicFolder} disabled={localImporting}><FolderOpen weight="fill" />{localImporting ? "正在读取…" : localTracks.length ? "更换文件夹" : localFolderAccess === "permission" ? "恢复读取" : "导入文件夹"}</button>
                      {localFolderAccess === "permission" && !localTracks.length ? <button className="is-secondary" type="button" onClick={importLocalMusicFolder}>更换位置</button> : null}
                    </div>
                    <input ref={localFolderInputRef} className="local-folder-input" type="file" accept="audio/*,.mp3,.m4a,.aac,.wav,.ogg,.oga,.flac,.opus" webkitdirectory="" directory="" multiple onChange={handleLocalFolderFiles} />
                  </div>
                )}
              </div>
            </section>

            <section className="settings-section settings-general-card general-preference-section general-user-data-card" aria-labelledby="general-user-data-title">
              <div className="settings-card-heading">
                <span><Database weight="fill" aria-hidden="true" /><strong id="general-user-data-title">用户数据导入与导出</strong></span>
                <small>Tomatotodo JSON 备份</small>
              </div>
              <div className="user-data-transfer-copy">
                <p>备份仪表盘布局、任务清单、个性化与常规设置、便笺，以及全部专注档案。</p>
                <small>为保护隐私，Spotify 登录令牌、本地音乐文件和文件夹授权不会导出。</small>
              </div>
              <div className="user-data-transfer-actions">
                <button type="button" onClick={downloadUserDataBackup}><DownloadSimple weight="bold" />导出 JSON</button>
                <button type="button" onClick={() => backupImportInputRef.current?.click()}><UploadSimple weight="bold" />导入 JSON</button>
                <input ref={backupImportInputRef} type="file" accept="application/json,.json" onChange={handleUserDataImportFile} />
              </div>
              {backupImportError ? <p className="user-data-import-error" role="alert">{backupImportError}</p> : null}
              {pendingImportBackup ? (
                <div className="user-data-import-confirmation" role="alert" aria-live="polite">
                  <div>
                    <strong>确认覆盖当前用户数据？</strong>
                    <small>
                      备份于 {new Date(pendingImportBackup.exportedAt).toLocaleString("zh-CN")} · {pendingImportBackup.data.configuration.taskLists.length} 个清单 · {pendingImportBackup.data.archive.focusLogs.length} 条专注记录 · {pendingImportBackup.data.notes.length} 条便笺
                    </small>
                  </div>
                  <span>
                    <button type="button" onClick={() => setPendingImportBackup(null)}>取消</button>
                    <button type="button" onClick={applyUserDataBackup}>确认导入并覆盖</button>
                  </span>
                </div>
              ) : null}
            </section>

            <section className={`archive-danger-zone settings-danger-zone general-preference-section step-${clearConfirmStep}`} aria-labelledby="settings-clear-archive-title">
              <div>
                <Trash weight="fill" aria-hidden="true" />
                <span>
                  <strong id="settings-clear-archive-title">恢复出厂数据</strong>
                  <small>
                    {clearConfirmStep === 0 && "清除用户数据，并恢复本次更新保存的 1.2 默认模板。"}
                    {clearConfirmStep === 1 && "第一次警告：当前任务与设置将被默认模板覆盖，档案和用户便笺将被清除。"}
                    {clearConfirmStep === 2 && "最终警告：数据清除无法撤销，本地媒体授权需要重新确认。"}
                  </small>
                </span>
              </div>
              <div className="archive-danger-actions">
                {clearConfirmStep > 0 ? <button type="button" onClick={() => setClearConfirmStep(0)}>取消</button> : null}
                <button type="button" onClick={advanceFactoryReset}>
                  {clearConfirmStep === 0 && "恢复出厂数据"}
                  {clearConfirmStep === 1 && "第一次确认"}
                  {clearConfirmStep === 2 && "最终确认并恢复"}
                </button>
              </div>
            </section>
          </div>
          ) : null}
        </div>
        {settingsTab === "tasks" && typeof document !== "undefined" ? createPortal(
          <button className="add-preset-floating" type="button" onClick={openCreatePreset}><Plus weight="bold" />添加预设</button>,
          document.querySelector(".app-shell") || document.body,
        ) : null}
        </div>
      ) : null}

      {dashboardVisible ? (
        <div className="dashboard-backdrop archive-workspace">
          <section
            className="dashboard-sheet"
            role="region"
            aria-labelledby="dashboard-title"
          >
            <div className="dashboard-scroll-body workspace-page-enter">
            <header className="dashboard-header">
              <div>
                <button className="workspace-title-action" type="button" onClick={() => scrollWorkspaceToTop("archive")}><h2 id="dashboard-title">{archiveCareMessage}</h2></button>
              </div>
              <div className="dashboard-grid-toolbar archive-export-toolbar">
                <button
                  className={`archive-export-button ${archiveExporting ? "is-exporting" : ""}`}
                  type="button"
                  onClick={exportArchiveAsImage}
                  disabled={archiveExporting}
                  aria-label={archiveExporting ? "正在导出档案图片" : "导出档案为图片"}
                  title="导出本页为图片"
                >
                  <DownloadSimple weight="bold" aria-hidden="true" />
                </button>
              </div>
            </header>

            <div className="metric-grid">
              <article>
                <Clock weight="duotone" aria-hidden="true" />
                <span>今日专注</span>
                <strong>
                  {trueTodaySeconds < 60 ? trueTodaySeconds : Math.floor(trueTodaySeconds / 60)}
                  <small>{trueTodaySeconds < 60 ? "秒" : "分钟"}</small>
                </strong>
              </article>
              <article className="tomato-metric">
                <Target weight="duotone" aria-hidden="true" />
                <span>完成番茄</span>
                <div className="tomato-row" aria-label={`今日完成 ${completedPomodoros} 个番茄`}>
                  {completedPomodoros ? Array.from({ length: Math.min(completedPomodoros, 10) }, (_, index) => (
                    <i className="tomato-mark" key={index} aria-hidden="true" />
                  )) : <small>完成一轮后，番茄会长在这里</small>}
                  {completedPomodoros > 10 ? <b>+{completedPomodoros - 10}</b> : null}
                </div>
              </article>
              <article>
                <ListChecks weight="duotone" aria-hidden="true" />
                <span>今日任务</span>
                <strong>{completedTasks}<small>/ {tasks.length}</small></strong>
              </article>
              <article>
                <TrendUp weight="duotone" aria-hidden="true" />
                <span>专注次数</span>
                <strong>{todayFocusSessions}<small>次</small></strong>
              </article>
            </div>

            <div className="dashboard-layout">
              <section className="dashboard-card calendar-card" aria-labelledby="calendar-title">
                <div className="card-heading">
                  <div><CalendarBlank weight="duotone" /><span>专注日历</span></div>
                  <div className="calendar-nav">
                    <button
                      type="button"
                      onClick={() => setCalendarMonth((current) => new Date(current.getFullYear(), current.getMonth() - 1, 1))}
                      aria-label="上个月"
                    ><CaretLeft /></button>
                    <strong id="calendar-title">{calendarMonth.getFullYear()} / {String(calendarMonth.getMonth() + 1).padStart(2, "0")}</strong>
                    <button
                      type="button"
                      onClick={() => setCalendarMonth((current) => new Date(current.getFullYear(), current.getMonth() + 1, 1))}
                      aria-label="下个月"
                    ><CaretRight /></button>
                  </div>
                </div>
                <div className="monthly-tomato-summary" aria-label={`本月获得 ${monthlyPomodoros} 个番茄`}>
                  <i className="tomato-mark" aria-hidden="true" />
                  <span>本月番茄</span>
                  <strong>{monthlyPomodoros}</strong>
                  <small>个</small>
                </div>
                <div className="calendar-weekdays" aria-hidden="true">
                  {"一二三四五六日".split("").map((day) => <span key={day}>{day}</span>)}
                </div>
                <div className="calendar-grid">
                  {calendarDays.map((day) => {
                    const key = toDateKey(day);
                    const loggedSeconds = focusLogs
                      .filter((log) => log.date === key)
                      .reduce((sum, log) => sum + log.seconds, 0);
                    const minutes = Math.round(loggedSeconds / 60);
                    const tomatoes = focusLogs.filter(
                      (log) => log.date === key && log.reason === "completed",
                    ).length;
                    const intensityLevel = focusIntensityLevel(loggedSeconds);
                    return (
                      <button
                        key={key}
                        className={`focus-level-${intensityLevel} ${selectedDate === key ? "is-selected" : ""} ${key === todayKey ? "is-today" : ""}`}
                        data-outside={day.getMonth() !== calendarMonth.getMonth()}
                        data-focus-level={intensityLevel}
                        data-tomatoes={`${tomatoes} 个番茄`}
                        type="button"
                        onClick={() => setSelectedDate(key)}
                        aria-label={`${key}，专注 ${minutes} 分钟，获得 ${tomatoes} 个番茄`}
                      >
                        <span>{day.getDate()}</span>
                        {minutes > 0 ? <i>{minutes}</i> : null}
                      </button>
                    );
                  })}
                </div>
              </section>

              <section className="dashboard-card focus-chart-card" aria-labelledby="focus-chart-title">
                <div className="focus-chart-heading">
                  <div>
                    <TrendUp weight="duotone" aria-hidden="true" />
                    <span>
                      <strong id="focus-chart-title">专注统计</strong>
                      <small>{focusChart.title}</small>
                    </span>
                  </div>
                  <div className="focus-chart-nav">
                    <button
                      type="button"
                      onClick={() => {
                        setHoveredChartPoint(null);
                        setStatsCursor((current) => shiftStatsCursor(current, statsGranularity, -1));
                      }}
                      aria-label={`上一${({ day: "天", week: "周", month: "月", year: "年" })[statsGranularity]}`}
                    ><CaretLeft /></button>
                    <button
                      type="button"
                      onClick={() => {
                        setHoveredChartPoint(null);
                        setStatsCursor((current) => shiftStatsCursor(current, statsGranularity, 1));
                      }}
                      aria-label={`下一${({ day: "天", week: "周", month: "月", year: "年" })[statsGranularity]}`}
                    ><CaretRight /></button>
                  </div>
                </div>
                <div className="focus-chart-tabs" role="group" aria-label="专注统计周期">
                  {[ ["day", "日度"], ["week", "周度"], ["month", "月度"], ["year", "年度"] ].map(([key, label]) => (
                    <button
                      className={statsGranularity === key ? "is-active" : ""}
                      type="button"
                      onClick={() => {
                        setHoveredChartPoint(null);
                        setStatsGranularity(key);
                        setStatsCursor(new Date());
                      }}
                      aria-pressed={statsGranularity === key}
                      key={key}
                    >{label}</button>
                  ))}
                </div>
                <div className="focus-chart-wrap">
                  <span className="focus-chart-y-title">专注时间 / 分钟</span>
                  <svg className="focus-line-chart" viewBox="0 0 720 260" role="img" aria-label={`${focusChart.title}专注时间折线图`}>
                    {focusChartScale.ticks.map((value) => {
                      const y = 214 - (value / focusChartCeiling) * 164;
                      return (
                        <g className="focus-chart-grid" key={value}>
                          <line x1="58" y1={y} x2="686" y2={y} />
                          <text x="48" y={y + 4}>{value}</text>
                        </g>
                      );
                    })}
                    <path className="focus-chart-area" d={`${focusChartPath} L${focusChartPoints.at(-1)?.x || 58},214 L58,214 Z`} />
                    <path className="focus-chart-line" d={focusChartPath} />
                    {focusChartPoints.map((point, index) => (
                      <g
                        className="focus-chart-point"
                        key={point.key}
                        tabIndex="0"
                        role="button"
                        aria-label={`${point.label}，专注 ${formatMinutes(Math.round(point.value * 60))}`}
                        onPointerEnter={() => setHoveredChartPoint({ ...point, index })}
                        onPointerLeave={() => setHoveredChartPoint(null)}
                        onFocus={() => setHoveredChartPoint({ ...point, index })}
                        onBlur={() => setHoveredChartPoint(null)}
                      >
                        <circle className="focus-chart-hit" cx={point.x} cy={point.y} r="12" />
                        <circle className="focus-chart-dot" cx={point.x} cy={point.y} r="4" />
                        {point.shortLabel ? <text x={point.x} y="239">{point.shortLabel}</text> : null}
                      </g>
                    ))}
                  </svg>
                  {hoveredChartPoint ? (
                    <div
                      className={`focus-chart-tooltip ${hoveredChartPoint.index < 2 ? "is-left-edge" : ""} ${hoveredChartPoint.index > focusChartPoints.length - 3 ? "is-right-edge" : ""}`}
                      style={{
                        left: `${(hoveredChartPoint.x / 720) * 100}%`,
                        top: `${(hoveredChartPoint.y / 260) * 100}%`,
                      }}
                      role="tooltip"
                    >
                      <strong>{hoveredChartPoint.label}</strong>
                      <span>专注 {formatMinutes(Math.round(hoveredChartPoint.value * 60))}</span>
                    </div>
                  ) : null}
                  <span className="focus-chart-x-title">日期</span>
                </div>
              </section>

              <section className="dashboard-card focus-heatmap-card" aria-labelledby="focus-heatmap-title">
                <div className="focus-heatmap-heading">
                  <div>
                    <ChartBar weight="duotone" aria-hidden="true" />
                    <span>
                      <strong id="focus-heatmap-title">专注热力图</strong>
                      <small>{heatmapYear} 年累计 {formatMinutes(heatmap.totalSeconds)}</small>
                    </span>
                  </div>
                  <div className="focus-chart-nav">
                    <button
                      type="button"
                      onClick={() => {
                        setHoveredHeatmapCell(null);
                        setHeatmapYear((year) => year - 1);
                      }}
                      aria-label="上一年"
                    ><CaretLeft /></button>
                    <button
                      type="button"
                      onClick={() => {
                        setHoveredHeatmapCell(null);
                        setHeatmapYear((year) => year + 1);
                      }}
                      aria-label="下一年"
                    ><CaretRight /></button>
                  </div>
                </div>
                <div className="focus-heatmap-scroll">
                  <div className="focus-heatmap-months" style={{ "--heatmap-weeks": heatmap.weeks }} aria-hidden="true">
                    {heatmap.months.map((month) => (
                      <span style={{ gridColumn: month.week + 1 }} key={month.label}>{month.label}</span>
                    ))}
                  </div>
                  <div className="focus-heatmap-body">
                    <div className="focus-heatmap-weekdays" aria-hidden="true">
                      <span style={{ gridRow: 2 }}>一</span>
                      <span style={{ gridRow: 4 }}>三</span>
                      <span style={{ gridRow: 6 }}>五</span>
                    </div>
                    <div className="focus-heatmap-grid" style={{ "--heatmap-weeks": heatmap.weeks }}>
                      {heatmap.cells.map((cell) => (
                        <button
                          className={`focus-heatmap-cell level-${cell.level}`}
                          style={{ gridColumn: cell.week + 1, gridRow: cell.weekday + 1 }}
                          type="button"
                          disabled={!cell.inYear}
                          tabIndex={cell.inYear ? 0 : -1}
                          aria-label={`${cell.date.toLocaleDateString("zh-CN")}，专注 ${formatMinutes(cell.seconds)}`}
                          onPointerEnter={() => cell.inYear && setHoveredHeatmapCell(cell)}
                          onPointerLeave={() => setHoveredHeatmapCell(null)}
                          onFocus={() => cell.inYear && setHoveredHeatmapCell(cell)}
                          onBlur={() => setHoveredHeatmapCell(null)}
                          key={cell.key}
                        />
                      ))}
                    </div>
                  </div>
                </div>
                <div className="focus-heatmap-footer">
                  <span>每格代表一天</span>
                  <div aria-label="专注时间颜色图例：50、100、150、200、250分钟五档">
                    <small>0</small>
                    {[0, 1, 2, 3, 4, 5].map((level) => <i className={`level-${level}`} key={level} />)}
                    <small>250+</small>
                  </div>
                </div>
                {hoveredHeatmapCell ? (
                  <div className="focus-heatmap-tooltip" role="tooltip">
                    <strong>{hoveredHeatmapCell.date.toLocaleDateString("zh-CN", { year: "numeric", month: "long", day: "numeric", weekday: "short" })}</strong>
                    <span>专注 {formatMinutes(hoveredHeatmapCell.seconds)}</span>
                  </div>
                ) : null}
              </section>

              <section className="dashboard-card day-log-card" aria-labelledby="day-log-title">
                <div className="card-heading">
                  <div><ChartBar weight="duotone" /><span id="day-log-title">{selectedDate} 日志</span></div>
                  <strong>{formatMinutes(selectedTrackedSeconds)}</strong>
                </div>
                <div className="day-log-list">
                  {selectedLogs.length ? selectedLogs.map((log) => (
                    <article className={`focus-level-${focusIntensityLevel(log.seconds)}`} key={log.id}>
                      <span>{log.time}</span>
                      <strong>{log.taskTitle}</strong>
                      <small>{formatMinutes(log.seconds)}</small>
                    </article>
                  )) : (
                    <div className="empty-log">
                      <Timer weight="duotone" aria-hidden="true" />
                      <p>{selectedTrackedSeconds ? `当日累计 ${formatMinutes(selectedTrackedSeconds)}。` : "这一天还没有专注记录。"}</p>
                    </div>
                  )}
                </div>
              </section>

            </div>
            </div>
          </section>
        </div>
      ) : null}

      {topNotice ? (
        <div
          className={`top-notice is-${topNotice.type} ${topNoticeDragging ? "is-dragging" : ""}`}
          role="status"
          key={topNotice.id}
          style={{ "--notice-offset": `${topNoticeOffset}px` }}
          onPointerDown={startTopNoticeSwipe}
          onPointerMove={moveTopNoticeSwipe}
          onPointerUp={finishTopNoticeSwipe}
          onPointerCancel={finishTopNoticeSwipe}
          tabIndex={0}
          onKeyDown={(event) => {
            if (event.key === "Escape") setTopNotice(null);
          }}
        >
          {topNotice.type === "tomato" ? <i className="tomato-mark" aria-hidden="true" /> : <Timer weight="duotone" aria-hidden="true" />}
          <span><strong>{topNotice.title}</strong><small>{topNotice.body}</small></span>
        </div>
      ) : null}

    </main>
  );
}
