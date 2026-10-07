const mysql = require('mysql2/promise');
const { DB } = require('./config');

// MySQL 연결 풀. 시각은 모두 UTC (스키마 규칙)
module.exports = mysql.createPool({ ...DB, timezone: 'Z' });
