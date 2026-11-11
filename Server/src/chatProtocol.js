const { MAX_NAME_LENGTH, MAX_CHAT_LENGTH, MAX_CELL } = require('./config');

// 채팅, 건물 메시지 (TCP, 한 줄에 JSON 하나)
//   클라 -> 서버  { t: 'join', name }              접속 후 처음 한 번
//                 { t: 'chat', text }
//                 { t: 'build', id, type, x, y }    건물 건설 (id 는 지은 클라이언트가 만듦, x, y 는 기준 칸)
//                 { t: 'demolish', id }             건물 철거
//                 { t: 'move', id, x, y }           건물 이동
//   서버 -> 클라  { t: 'chat', name, text }        모든 접속자에게 전달
//                 { t: 'kick' }                    같은 사용자명이 다른 곳에서 접속해 끊김
//                 { t: 'buildings', list: [{ id, type, x, y, owner }] }  입장하면 지금 맵에 있는 건물 전부
//                 { t: 'build', id, type, x, y, owner }, { t: 'demolish', id }, { t: 'move', id, x, y }
//                                                  다른 사람이 바꾼 건물 (바꾼 사람에게는 보내지 않음)

const isId = v => typeof v === 'string' && /^[A-Za-z0-9_-]{1,64}$/.test(v);
const isCell = v => Number.isInteger(v) && Math.abs(v) <= MAX_CELL;

// 잘못된 메시지면 null
function parse(line) {
  let m;
  try {
    m = JSON.parse(line);
  } catch {
    return null;
  }
  if (!m) return null;

  if (m.t === 'join' && typeof m.name === 'string' && m.name.length > 0 && m.name.length <= MAX_NAME_LENGTH) {
    return { t: 'join', name: m.name };
  }
  if (m.t === 'chat' && typeof m.text === 'string') {
    const text = m.text.trim();
    if (text.length > 0 && text.length <= MAX_CHAT_LENGTH) return { t: 'chat', text };
  }
  if (m.t === 'build' && isId(m.id) && isId(m.type) && isCell(m.x) && isCell(m.y)) {
    return { t: 'build', id: m.id, type: m.type, x: m.x, y: m.y };
  }
  if (m.t === 'demolish' && isId(m.id)) return { t: 'demolish', id: m.id };
  if (m.t === 'move' && isId(m.id) && isCell(m.x) && isCell(m.y)) return { t: 'move', id: m.id, x: m.x, y: m.y };
  return null;
}

const toLine = obj => JSON.stringify(obj) + '\n';
const chat = (name, text) => toLine({ t: 'chat', name, text });
const kick = () => toLine({ t: 'kick' });
const buildingList = list => toLine({ t: 'buildings', list });
const build = b => toLine({ t: 'build', id: b.id, type: b.type, x: b.x, y: b.y, owner: b.owner });
const demolish = id => toLine({ t: 'demolish', id });
const move = b => toLine({ t: 'move', id: b.id, x: b.x, y: b.y });

module.exports = { parse, chat, kick, buildingList, build, demolish, move };
