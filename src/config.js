const path = require('path');

// .env 파일이 있으면 환경 변수로 읽음 (먼저 읽은 값이 우선, 둘 다 git 에 올라가지 않음)
//   .env    : 서버 설정을 바꿀 때만 (PORT, PUBLIC_URL, STEAM_API_KEY 등), 없어도 됨
//   DB/.env : DB 비밀번호, docker-compose.yml 과 같이 쓴다 (DB/.env_example 참고)
for (const file of ['.env', 'DB/.env']) {
  try {
    process.loadEnvFile(path.join(__dirname, '..', file));
  } catch (err) {
    if (err.code !== 'ENOENT') throw err;
  }
}

const PORT = Number(process.env.PORT) || 7777;

// 서버 설정값
module.exports = {
  PORT,                                             // UDP(게임), HTTP(웹 확인, 로그인) 공통 포트
  CHAT_PORT: Number(process.env.CHAT_PORT) || 7778, // TCP(채팅) 포트
  TIMEOUT_MS: 5000,        // 이 시간 동안 소식이 없으면 접속 종료로 처리
  PRUNE_INTERVAL_MS: 1000, // 접속 종료 검사 주기
  KICKED_TTL_MS: 60000,    // 끊긴 세션 ID를 기억하는 시간
  MAX_NAME_LENGTH: 32,     // 사용자명 최대 길이
  MAX_CHAT_LENGTH: 200,    // 채팅 메시지 최대 길이
  MAX_LINE_LENGTH: 4096,   // 채팅(TCP) 한 줄 최대 길이, 넘으면 연결을 끊음

  // 로그인
  PUBLIC_URL: (process.env.PUBLIC_URL || `http://localhost:${PORT}`).replace(/\/+$/, ''), // 클라이언트 브라우저가 접속할 수 있는 이 서버 주소, Steam 로그인 후 여기로 돌아옴
  STEAM_API_KEY: process.env.STEAM_API_KEY || '', // 있으면 가입할 때 Steam 프로필 이름을 닉네임으로 사용
  LOGIN_TTL_MS: 10 * 60 * 1000, // Steam 로그인을 끝내야 하는 시간
  MAX_PENDING_LOGINS: 1000,     // 동시에 진행 중일 수 있는 로그인 수
  SESSION_TTL_DAYS: 30,         // 세션 토큰 유효 기간
  MAX_NICKNAME_LENGTH: 20,      // DB users.nickname 최대 길이

  // MySQL (DB/docker-compose.yml)
  DB: {
    host: process.env.DB_HOST || '127.0.0.1',
    port: Number(process.env.DB_PORT) || 3307,
    user: process.env.DB_USER || 'village',
    password: process.env.DB_PASSWORD || process.env.MYSQL_PASSWORD || '', // 따로 정하지 않으면 DB/.env 의 MYSQL_PASSWORD
    database: process.env.DB_NAME || 'village',
  },
};
