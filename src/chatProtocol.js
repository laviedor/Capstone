const { MAX_NAME_LENGTH, MAX_CHAT_LENGTH } = require('./config');

// 채팅 메시지 (TCP, 한 줄에 JSON 하나)
//   클라 -> 서버  { t: 'join', name }        접속 후 처음 한 번
//                 { t: 'chat', text }
//   서버 -> 클라  { t: 'chat', name, text }  모든 접속자에게 전달
//                 { t: 'kick' }              같은 사용자명이 다른 곳에서 접속해 끊김

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
  return null;
}

const toLine = obj => JSON.stringify(obj) + '\n';
const chat = (name, text) => toLine({ t: 'chat', name, text });
const kick = () => toLine({ t: 'kick' });

module.exports = { parse, chat, kick };
