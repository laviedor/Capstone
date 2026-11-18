const express = require('express');
const friends = require('./friends');
const accounts = require('./accounts');
const presence = require('./presence');
const chatProtocol = require('./chatProtocol');
const worlds = require('./worlds');
const { sessionUser, wrap, errorHandler } = require('./httpUtil');
const { MAX_NICKNAME_LENGTH, MAX_CHAT_LENGTH } = require('./config');

// 친구, 1:1 채팅 API (HTTP, JSON). webServer.js 에서 /friends 아래에 붙인다
// 모두 Authorization: Bearer 토큰이 필요하다 (없거나 만료됐으면 401)
//   GET    /friends   -> { friends: [{ id, nickname, online, unread }], incoming: [{ id, nickname }], outgoing: [{ id, nickname }] }
//            online: 지금 접속 중인지, unread: 그 친구가 보낸 1:1 채팅 중 안 읽은 개수
//            incoming: 나에게 친구 요청을 보낸 사람들, outgoing: 내가 친구 요청을 보내 둔 사람들 (둘 다 아직 수락, 거절 전)
//   POST   /friends              { nickname } 닉네임으로 친구 요청 보내기 (상대가 수락해야 친구가 됨)
//            -> { status: 'requested', friend: { id, nickname } }   요청을 보냄
//               { status: 'ok', friend: { id, nickname } }          상대가 먼저 보낸 요청이 있어서 바로 친구가 됨
//               404 { status: 'not_found' }   그런 닉네임이 없음
//               400 { status: 'self' }        자기 자신
//               409 { status: 'already' }     이미 친구
//               409 { status: 'pending' }     이미 요청을 보내 둠
//               409 { status: 'full' }        친구 수가 가득 참
//               409 { status: 'too_many' }    보내 둔 요청이 너무 많거나, 상대가 받아 둔 요청이 너무 많음
//               409 { status: 'ambiguous' }   같은 닉네임을 쓰는 유저가 여럿
//   POST   /friends/requests/:id/accept      유저 :id 가 나에게 보낸 친구 요청 수락
//            -> { status: 'ok', friend: { id, nickname } }
//               404 { status: 'none' }        그런 요청이 없음 (상대가 취소했거나 탈퇴함)
//               409 { status: 'full' }        둘 중 한 명의 친구 수가 가득 참
//   POST   /friends/requests/:id/reject      유저 :id 가 나에게 보낸 친구 요청 거절 -> { ok: true }
//   POST   /friends/requests/:id/cancel      내가 유저 :id 에게 보낸 친구 요청 취소 -> { ok: true }
//   DELETE /friends/:id                      친구 삭제 (서로의 목록에서 지워짐, 마을 구경 중이면 돌려보냄) -> { ok: true }
//   GET    /friends/:id/messages?after=ID    그 친구와의 1:1 채팅 (ID 뒤의 것만, 없으면 최근 것), 읽음 처리
//            -> { status: 'ok', messages: [{ id, sender, text }] }   sender 는 보낸 유저 id
//               403 { status: 'not_friend' }
//   POST   /friends/:id/messages { text }    1:1 채팅 보내기
//            -> { status: 'ok', message: { id, sender, text } }
//               403 { status: 'not_friend' } / 400 { status: 'invalid' }
//
// 상대가 접속 중이면 채팅 서버(TCP) 연결로 알림을 보낸다 (chatProtocol.js 의 dm, friends).
// 알림에는 내용이 없고, 받은 클라이언트가 위 API 로 새 메시지와 친구 목록, 친구 요청을 다시 가져간다

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
  const [list, requests] = await Promise.all([friends.list(req.user.id), friends.requests(req.user.id)]);
  res.json({
    friends: list.map(f => ({ ...f, online: presence.isOnline(f.id) })),
    incoming: requests.incoming,
    outgoing: requests.outgoing,
  });
}));

router.post('/', wrap(async (req, res) => {
  const nickname = typeof req.body?.nickname === 'string' ? req.body.nickname.normalize('NFC').trim() : '';
  if (!nickname || [...nickname].length > MAX_NICKNAME_LENGTH) return res.status(404).json({ status: 'not_found' });

  const target = await friends.findByNickname(nickname);
  if (!target) return res.status(404).json({ status: 'not_found' });
  if (target === 'ambiguous') return res.status(409).json({ status: 'ambiguous' });
  if (target.id === req.user.id) return res.status(400).json({ status: 'self' });

  const result = await friends.request(req.user.id, target.id);
  if (result === 'gone') return res.status(404).json({ status: 'not_found' });
  if (result !== 'requested' && result !== 'accepted') return res.status(409).json({ status: result });

  presence.send(target.id, chatProtocol.friends()); // 상대 화면에 받은 요청(또는 새 친구)이 보이도록
  if (result === 'accepted') {
    console.log(`[친구] ${req.user.nickname} + ${target.nickname} (서로 요청)`);
    return res.json({ status: 'ok', friend: target });
  }
  console.log(`[친구] ${req.user.nickname} -> ${target.nickname} 요청`);
  res.json({ status: 'requested', friend: target });
}));

router.post('/requests/:id/accept', wrap(async (req, res) => {
  const result = await friends.accept(req.user.id, req.friendId);
  if (result === 'none' || result === 'gone') return res.status(404).json({ status: 'none' });
  if (result !== 'ok') return res.status(409).json({ status: result });

  const friend = await accounts.findUser(req.friendId);
  presence.send(req.friendId, chatProtocol.friends()); // 요청을 보낸 사람 화면에 새 친구가 보이도록
  console.log(`[친구] ${friend?.nickname ?? '#' + req.friendId} + ${req.user.nickname} (수락)`);
  res.json({ status: 'ok', friend });
}));

router.post('/requests/:id/reject', wrap(async (req, res) => {
  if (await friends.removeRequest(req.friendId, req.user.id)) {
    presence.send(req.friendId, chatProtocol.friends()); // 요청을 보낸 사람의 보낸 요청 목록에서 사라지도록
    console.log(`[친구] ${req.user.nickname} 거절 #${req.friendId}`);
  }
  res.json({ ok: true });
}));

router.post('/requests/:id/cancel', wrap(async (req, res) => {
  if (await friends.removeRequest(req.user.id, req.friendId)) {
    presence.send(req.friendId, chatProtocol.friends()); // 받은 사람의 받은 요청 목록에서 사라지도록
    console.log(`[친구] ${req.user.nickname} 요청 취소 #${req.friendId}`);
  }
  res.json({ ok: true });
}));

router.delete('/:id', wrap(async (req, res) => {
  if (await friends.remove(req.user.id, req.friendId)) {
    presence.send(req.friendId, chatProtocol.friends());
    console.log(`[친구] ${req.user.nickname} - #${req.friendId}`);
    // 서로의 마을을 구경하던 중이면 자기 마을로 돌려보낸다 (친구만 방문할 수 있으므로)
    await worlds.evict(req.user.id, req.friendId);
    await worlds.evict(req.friendId, req.user.id);
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
