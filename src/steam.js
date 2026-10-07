const { STEAM_API_KEY } = require('./config');

// Steam OpenID 2.0 로그인 ("Sign in through Steam")
// AppID, 퍼블리셔 키 없이 SteamID64 를 확인할 수 있다

const OPENID_URL = 'https://steamcommunity.com/openid/login';
const OPENID_NS = 'http://specs.openid.net/auth/2.0';
const IDENTIFIER_SELECT = 'http://specs.openid.net/auth/2.0/identifier_select';
const CLAIMED_ID = /^https:\/\/steamcommunity\.com\/openid\/id\/(\d{17})$/;
const MUST_BE_SIGNED = ['op_endpoint', 'claimed_id', 'identity', 'return_to', 'response_nonce'];

// 브라우저로 열 Steam 로그인 주소. 로그인이 끝나면 Steam 이 returnTo 로 돌려보낸다
function loginUrl(returnTo, realm) {
  const q = new URLSearchParams({
    'openid.ns': OPENID_NS,
    'openid.mode': 'checkid_setup',
    'openid.return_to': returnTo,
    'openid.realm': realm,
    'openid.identity': IDENTIFIER_SELECT,
    'openid.claimed_id': IDENTIFIER_SELECT,
  });
  return `${OPENID_URL}?${q}`;
}

// Steam 이 돌려보낸 쿼리(openid.*)를 검증하고 SteamID64(문자열)를 반환, 실패하면 null
//   returnTo 가 다르면 거절: 다른 사이트의 Steam 로그인 응답을 가져와 재사용하는 것을 막는다
async function verify(query, returnTo) {
  const get = key => query.get('openid.' + key) || '';
  if (get('ns') !== OPENID_NS || get('mode') !== 'id_res') return null;
  if (get('op_endpoint') !== OPENID_URL || get('return_to') !== returnTo) return null;
  const m = CLAIMED_ID.exec(get('claimed_id'));
  if (!m || get('identity') !== get('claimed_id')) return null;
  const signed = get('signed').split(',');
  if (!MUST_BE_SIGNED.every(key => signed.includes(key))) return null;

  // 서명이 진짜인지 Steam 에 직접 확인 (check_authentication)
  const body = new URLSearchParams();
  for (const [key, value] of query) {
    if (key.startsWith('openid.') && !body.has(key)) body.append(key, value);
  }
  body.set('openid.mode', 'check_authentication');
  const res = await fetch(OPENID_URL, { method: 'POST', body, signal: AbortSignal.timeout(10000) });
  const text = await res.text();
  return res.ok && /^is_valid:true$/m.test(text) ? m[1] : null;
}

// Steam 프로필 이름 (STEAM_API_KEY 가 있을 때만), 못 가져오면 null
async function fetchPersonaName(steamId) {
  if (!STEAM_API_KEY) return null;
  try {
    const q = new URLSearchParams({ key: STEAM_API_KEY, steamids: steamId });
    const res = await fetch(`https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?${q}`, {
      signal: AbortSignal.timeout(5000),
    });
    if (!res.ok) return null;
    const name = (await res.json()).response?.players?.[0]?.personaname;
    return typeof name === 'string' ? name : null;
  } catch {
    return null;
  }
}

module.exports = { loginUrl, verify, fetchPersonaName };
