// 서버 설정값
module.exports = {
  PORT: Number(process.env.PORT) || 7777,           // UDP(게임), HTTP(웹 확인) 공통 포트
  CHAT_PORT: Number(process.env.CHAT_PORT) || 7778, // TCP(채팅) 포트
  TIMEOUT_MS: 5000,        // 이 시간 동안 소식이 없으면 접속 종료로 처리
  PRUNE_INTERVAL_MS: 1000, // 접속 종료 검사 주기
  KICKED_TTL_MS: 60000,    // 끊긴 세션 ID를 기억하는 시간
  MAX_NAME_LENGTH: 32,     // 사용자명 최대 길이
  MAX_CHAT_LENGTH: 200,    // 채팅 메시지 최대 길이
  MAX_LINE_LENGTH: 4096,   // 채팅(TCP) 한 줄 최대 길이, 넘으면 연결을 끊음
};
