-- Reads a lock and its remaining TTL atomically.
-- KEYS[1] = lock key
-- Returns nil when free, otherwise { pttl, owner, service, instance, acquired, fence }  (pttl = -1: no expiry)
local h = redis.call('HMGET', KEYS[1], 'owner', 'service', 'instance', 'acquired', 'fence')
if not h[1] then
  return nil
end
return { redis.call('PTTL', KEYS[1]), h[1], h[2] or '', h[3] or '', h[4] or '0', h[5] or '0' }
