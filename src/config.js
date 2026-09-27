// 서버 설정값
module.exports = {
  PORT: Number(process.env.PORT) || 7777, // UDP(게임), HTTP(웹 확인) 공통 포트
  TIMEOUT_MS: 5000,        // 이 시간 동안 소식이 없으면 접속 종료로 처리
  PRUNE_INTERVAL_MS: 1000, // 접속 종료 검사 주기
  KICKED_TTL_MS: 60000,    // 끊긴 세션 ID를 기억하는 시간
};
