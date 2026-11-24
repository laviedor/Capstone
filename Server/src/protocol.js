// 클라이언트와 주고받는 메시지 (UDP, JSON)
//   클라 -> 서버  { t: 'pos', id: 세션ID, name: 사용자명, w: 월드 id, zn: 구역, x, y, z, n }
//                   w 는 지금 있는 월드 (없으면 0: 멀티버스), zn 은 그 안의 구역 (없으면 '': 마을)
//                   채팅 서버(TCP)에 등록된 월드, 구역과 같아야 전달된다
//                   n 은 보낸 순서 (새 클라이언트만 보냄). 같은 좌표가 채팅 연결(TCP)로도 오므로 받는 쪽이 이것으로 순서를 맞춘다
//   서버 -> 클라  { t: 'pos', name, x, y, z, n }   같은 월드, 같은 구역에 있는 다른 플레이어 좌표 (n 은 보낸 쪽이 붙였을 때만)
//                 { t: 'kick' }                 같은 사용자명이 다른 곳에서 접속해 끊김
//   UDP 가 닿지 않는 경우에 대비해 같은 좌표를 채팅 연결로도 주고받는다 (positions.js)

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
  const n = Number.isSafeInteger(m.n) && m.n > 0 ? m.n : 0;
  return { id: m.id, name: m.name, world, zone, x: m.x, y: m.y, z: m.z, n };
}

function isText(v, max) {
  return typeof v === 'string' && v.length > 0 && v.length <= max;
}

const position = p => JSON.stringify(
  p.n ? { t: 'pos', name: p.name, x: p.x, y: p.y, z: p.z, n: p.n } : { t: 'pos', name: p.name, x: p.x, y: p.y, z: p.z },
);
const kick = () => JSON.stringify({ t: 'kick' });

module.exports = { parse, position, kick };
