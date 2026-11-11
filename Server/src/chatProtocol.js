const { MAX_NAME_LENGTH, MAX_CHAT_LENGTH, MAX_CELL, MAX_TOKEN_LENGTH } = require('./config');

// 채팅, 건물 메시지 (TCP, 한 줄에 JSON 하나)
//   클라 -> 서버  { t: 'join', name, token }       접속 후 처음 한 번
//                                                  token(로그인 세션 토큰)이 맞으면 그 계정의 닉네임으로 입장하고
//                                                  1:1 채팅, 친구 알림을 받는다. 없거나 틀리면 name 으로 광장 채팅만
//                 { t: 'chat', text }
//                 { t: 'build', id, type, x, y }    건물 건설 (id 는 지은 클라이언트가 만듦, x, y 는 기준 칸)
//                 { t: 'demolish', id }             건물 철거
//                 { t: 'move', id, x, y }           건물 이동
//   서버 -> 클라  { t: 'chat', name, text }        모든 접속자에게 전달
//                 { t: 'kick' }                    같은 사용자명이 다른 곳에서 접속해 끊김
//                 { t: 'buildings', list: [{ id, type, x, y, owner }] }  입장하면 지금 맵에 있는 건물 전부
//                 { t: 'build', id, type, x, y, owner }, { t: 'demolish', id }, { t: 'move', id, x, y }
//                                                  다른 사람이 바꾼 건물 (바꾼 사람에게는 보내지 않음)
//                 { t: 'dm', sender }              친구(유저 id sender)가 1:1 채팅을 보냄. 그 사람에게만 가고
//                                                  내용은 HTTP 로 받아 간다 (friendRoutes.js)
//                 { t: 'friends' }                 친구 목록이나 친구의 접속 상태가 바뀜 (목록을 다시 받아 가라는 알림)

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
    const hasToken = typeof m.token === 'string' && m.token.length > 0 && m.token.length <= MAX_TOKEN_LENGTH;
    return { t: 'join', name: m.name, token: hasToken ? m.token : null };
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
const dm = senderId => toLine({ t: 'dm', sender: senderId });
const friends = () => toLine({ t: 'friends' });

module.exports = { parse, chat, kick, buildingList, build, demolish, move, dm, friends };
