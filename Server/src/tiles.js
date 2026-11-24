const pool = require('./db');
const { MAX_TILES } = require('./config');

// 마을 바닥 타일 DB 처리 (삽으로 바꾼 칸). 서버를 다시 켜도 남아 있다
//   잔디가 기본 바닥이라 저장하지 않고, 잔디가 아닌 칸만 village_tiles 에 저장한다
//   바닥은 개인 마을의 바깥(마을 구역)에만 바꾼다. 광장·집 안·필드는 바꾸지 않는다 (chatServer.js 가 확인)

// 바꿀 수 있는 바닥 종류 ('grass' 는 기본 바닥으로 되돌리기)
const GRASS = 'grass';
const TILE_TYPES = ['dirt'];

// DB 에 village_tiles 테이블이 없으면 (09_village_tiles.sql 을 아직 실행하지 않음) 바닥 기능만 끄고 나머지는 그대로 동작한다
const NO_TABLE = 'ER_NO_SUCH_TABLE';
let warned = false;
function missingTable(err) {
  if (err.code !== NO_TABLE) return false;
  if (!warned) {
    warned = true;
    console.warn('[바닥] village_tiles 테이블이 없습니다. DB/db/09_village_tiles.sql 을 실행해 주세요. (그 전까지 바닥 바꾸기는 꺼집니다)');
  }
  return true;
}

// 그 월드의 바뀐 바닥 칸 [{ x, y, tile }]
async function list(world) {
  try {
    const [rows] = await pool.query('SELECT x, y, tile FROM village_tiles WHERE village_id = ?', [world]);
    return rows.filter(row => TILE_TYPES.includes(row.tile));
  } catch (err) {
    if (missingTable(err)) return [];
    throw err;
  }
}

// 한 칸을 바꿈 -> 'ok' | 'unknown'(모르는 바닥 종류) | 'full'(바꾼 칸이 너무 많음) | 'nodb'(테이블 없음)
async function set(world, x, y, tile) {
  try {
    return await setTile(world, x, y, tile);
  } catch (err) {
    if (missingTable(err)) return 'nodb';
    throw err;
  }
}

async function setTile(world, x, y, tile) {
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
