// 클라이언트와 주고받는 메시지 (JSON)
//   클라 -> 서버  { t: 'pos', id: 세션ID, name: 사용자명, x, y, z }
//   서버 -> 클라  { t: 'pos', name, x, y, z }   다른 플레이어 좌표
//                 { t: 'kick' }                 같은 사용자명이 다른 곳에서 접속해 끊김

const MAX_NAME = 32;
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
  if (!isText(m.id, MAX_ID) || !isText(m.name, MAX_NAME)) return null;
  if (![m.x, m.y, m.z].every(Number.isFinite)) return null;
  return { id: m.id, name: m.name, x: m.x, y: m.y, z: m.z };
}

function isText(v, max) {
  return typeof v === 'string' && v.length > 0 && v.length <= max;
}

const position = p => JSON.stringify({ t: 'pos', name: p.name, x: p.x, y: p.y, z: p.z });
const kick = () => JSON.stringify({ t: 'kick' });

module.exports = { parse, position, kick };
