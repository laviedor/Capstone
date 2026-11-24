const pool = require('./db');
const { MAX_TILES } = require('./config');

// 마을 바닥 타일 DB 처리 (삽으로 바꾼 칸). 서버를 다시 켜도 남아 있다
//   잔디가 기본 바닥이라 저장하지 않고, 잔디가 아닌 칸만 village_tiles 에 저장한다
//   바닥은 개인 마을의 바깥(마을 구역)에만 바꾼다. 멀티버스·집 안·필드는 바꾸지 않는다 (chatServer.js 가 확인)

// 바꿀 수 있는 바닥 종류 ('grass' 는 기본 바닥으로 되돌리기)
const GRASS = 'grass';
const TILE_TYPES = ['dirt'];

// 그 월드의 바뀐 바닥 칸 [{ x, y, tile }]
async function list(world) {
  const [rows] = await pool.query('SELECT x, y, tile FROM village_tiles WHERE village_id = ?', [world]);
  return rows.filter(row => TILE_TYPES.includes(row.tile));
}

// 한 칸을 바꿈 -> 'ok' | 'unknown'(모르는 바닥 종류) | 'full'(바꾼 칸이 너무 많음)
async function set(world, x, y, tile) {
  if (tile === GRASS) {
    await pool.query('DELETE FROM village_tiles WHERE village_id = ? AND x = ? AND y = ?', [world, x, y]);
    return 'ok';
  }
  if (!TILE_TYPES.includes(tile)) return 'unknown';

  const [[{ n }]] = await pool.query('SELECT COUNT(*) AS n FROM village_tiles WHERE village_id = ?', [world]);
  if (n >= MAX_TILES) {
    const [same] = await pool.query('SELECT 1 FROM village_tiles WHERE village_id = ? AND x = ? AND y = ?', [world, x, y]);
    if (!same.length) return 'full';
  }

  await pool.query(
    'INSERT INTO village_tiles (village_id, x, y, tile) VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE tile = VALUES(tile)',
    [world, x, y, tile],
  );
  return 'ok';
}

module.exports = { list, set, GRASS, TILE_TYPES };
