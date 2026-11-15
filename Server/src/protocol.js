// 클라이언트와 주고받는 메시지 (UDP, JSON)
//   클라 -> 서버  { t: 'pos', id: 세션ID, name: 사용자명, w: 월드 id, zn: 구역, x, y, z }
//                   w 는 지금 있는 월드 (없으면 0: 멀티버스), zn 은 그 안의 구역 (없으면 '': 마을)
//                   채팅 서버(TCP)에 등록된 월드, 구역과 같아야 전달된다
//   서버 -> 클라  { t: 'pos', name, x, y, z }   같은 월드, 같은 구역에 있는 다른 플레이어 좌표
//                 { t: 'kick' }                 같은 사용자명이 다른 곳에서 접속해 끊김

const { MAX_NAME_LENGTH, MAX_ZONE_LENGTH } = require('./config');

const MAX_ID = 64;

// 잘못된 메시지면 null
function parse(buf) {
  let m;
  try {
    m = JSON.parse(buf.toString());
  } catch {
    return null;
  }
  if (!m || m.t !== 'pos') return null;
  if (!isText(m.id, MAX_ID) || !isText(m.name, MAX_NAME_LENGTH)) return null;
  if (![m.x, m.y, m.z].every(Number.isFinite)) return null;
  const world = m.w === undefined ? 0 : m.w;
  if (!Number.isSafeInteger(world) || world < 0) return null;
  const zone = m.zn === undefined ? '' : m.zn;
  if (typeof zone !== 'string' || zone.length > MAX_ZONE_LENGTH) return null;
  return { id: m.id, name: m.name, world, zone, x: m.x, y: m.y, z: m.z };
}

function isText(v, max) {
  return typeof v === 'string' && v.length > 0 && v.length <= max;
}

const position = p => JSON.stringify({ t: 'pos', name: p.name, x: p.x, y: p.y, z: p.z });
const kick = () => JSON.stringify({ t: 'kick' });

module.exports = { parse, position, kick };
