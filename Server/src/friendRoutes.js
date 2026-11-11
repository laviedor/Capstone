const express = require('express');
const friends = require('./friends');
const presence = require('./presence');
const chatProtocol = require('./chatProtocol');
const { sessionUser, wrap, errorHandler } = require('./httpUtil');
const { MAX_NICKNAME_LENGTH, MAX_CHAT_LENGTH } = require('./config');

// 친구, 1:1 채팅 API (HTTP, JSON). webServer.js 에서 /friends 아래에 붙인다
// 모두 Authorization: Bearer 토큰이 필요하다 (없거나 만료됐으면 401)
//   GET    /friends                          -> { friends: [{ id, nickname, online, unread }] }
//            online: 지금 접속 중인지, unread: 그 친구가 보낸 1:1 채팅 중 안 읽은 개수
//   POST   /friends              { nickname } 닉네임으로 친구 추가 (바로 서로 친구가 됨)
//            -> { status: 'ok', friend: { id, nickname } }
//               404 { status: 'not_found' }   그런 닉네임이 없음
//               400 { status: 'self' }        자기 자신
//               409 { status: 'already' }     이미 친구
//               409 { status: 'full' }        친구 수가 가득 참
//               409 { status: 'ambiguous' }   같은 닉네임을 쓰는 유저가 여럿
//   DELETE /friends/:id                      친구 삭제 (서로의 목록에서 지워짐) -> { ok: true }
//   GET    /friends/:id/messages?after=ID    그 친구와의 1:1 채팅 (ID 뒤의 것만, 없으면 최근 것), 읽음 처리
//            -> { status: 'ok', messages: [{ id, sender, text }] }   sender 는 보낸 유저 id
//               403 { status: 'not_friend' }
//   POST   /friends/:id/messages { text }    1:1 채팅 보내기
//            -> { status: 'ok', message: { id, sender, text } }
//               403 { status: 'not_friend' } / 400 { status: 'invalid' }
//
// 상대가 접속 중이면 채팅 서버(TCP) 연결로 알림을 보낸다 (chatProtocol.js 의 dm, friends).
// 알림에는 내용이 없고, 받은 클라이언트가 위 API 로 새 메시지와 친구 목록을 다시 가져간다

const ID = /^[1-9]\d{0,14}$/;

const router = express.Router();
router.use(express.json());

// 로그인 확인
router.use(wrap(async (req, res, next) => {
  req.user = await sessionUser(req);
  if (!req.user) return res.status(401).json({ error: 'UNAUTHORIZED' });
  next();
}));

router.param('id', (req, res, next, value) => {
  if (!ID.test(value)) return res.status(400).json({ status: 'invalid' });
  req.friendId = Number(value);
  next();
});

router.get('/', wrap(async (req, res) => {
  const list = await friends.list(req.user.id);
  res.json({ friends: list.map(f => ({ ...f, online: presence.isOnline(f.id) })) });
}));

router.post('/', wrap(async (req, res) => {
  const nickname = typeof req.body?.nickname === 'string' ? req.body.nickname.normalize('NFC').trim() : '';
  if (!nickname || [...nickname].length > MAX_NICKNAME_LENGTH) return res.status(404).json({ status: 'not_found' });

  const target = await friends.findByNickname(nickname);
  if (!target) return res.status(404).json({ status: 'not_found' });
  if (target === 'ambiguous') return res.status(409).json({ status: 'ambiguous' });
  if (target.id === req.user.id) return res.status(400).json({ status: 'self' });

  const result = await friends.add(req.user.id, target.id);
  if (result !== 'ok') return res.status(409).json({ status: result });

  presence.send(target.id, chatProtocol.friends()); // 상대 화면의 친구 목록도 바뀌도록
  console.log(`[친구] ${req.user.nickname} + ${target.nickname}`);
  res.json({ status: 'ok', friend: target });
}));

router.delete('/:id', wrap(async (req, res) => {
  if (await friends.remove(req.user.id, req.friendId)) {
    presence.send(req.friendId, chatProtocol.friends());
    console.log(`[친구] ${req.user.nickname} - #${req.friendId}`);
  }
  res.json({ ok: true });
}));

router.get('/:id/messages', wrap(async (req, res) => {
  if (!await friends.areFriends(req.user.id, req.friendId)) return res.status(403).json({ status: 'not_friend' });
  const after = typeof req.query.after === 'string' && ID.test(req.query.after) ? Number(req.query.after) : 0;
  res.json({ status: 'ok', messages: await friends.messages(req.user.id, req.friendId, after) });
}));

router.post('/:id/messages', wrap(async (req, res) => {
  const text = typeof req.body?.text === 'string' ? req.body.text.trim() : '';
  if (!text || text.length > MAX_CHAT_LENGTH) return res.status(400).json({ status: 'invalid' });
  if (!await friends.areFriends(req.user.id, req.friendId)) return res.status(403).json({ status: 'not_friend' });

  const message = await friends.addMessage(req.user.id, req.friendId, text);
  presence.send(req.friendId, chatProtocol.dm(req.user.id)); // 내용은 로그에 남기지 않는다
  res.json({ status: 'ok', message });
}));

router.use(errorHandler('친구'));

module.exports = router;
