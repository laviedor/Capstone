// 채팅 서버(TCP)에 로그인 토큰으로 접속해 있는 유저: 유저 id -> socket
//   친구 목록의 접속 표시, 1:1 채팅과 친구 변경 알림을 그 사람에게만 보내는 데 쓴다
const sockets = new Map();

module.exports = {
  add(userId, socket) {
    sockets.set(userId, socket);
  },

  // 이 접속이 등록되어 있었으면 지우고 true (다른 곳에서 새로 접속해 바뀐 뒤면 false)
  remove(userId, socket) {
    if (sockets.get(userId) !== socket) return false;
    sockets.delete(userId);
    return true;
  },

  isOnline: userId => sockets.has(userId),

  // 접속 중이면 보내고 true
  send(userId, line) {
    const socket = sockets.get(userId);
    if (!socket) return false;
    socket.write(line);
    return true;
  },
};
