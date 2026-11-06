-- Atomic, idempotent lock acquisition on ONE Redis node (the Redlock client runs it on every node).
-- KEYS[1] = lock key (hash)       KEYS[2] = fencing token counter (monotonic per node)
-- ARGV[1] = session id (owner)    ARGV[2] = lease ttl in ms
-- ARGV[3] = service name          ARGV[4] = instance id          ARGV[5] = now (unix ms)
-- Returns { status, fence | holder pttl, owner }
--   status 1 = acquired, 2 = already owned by the same session (ttl refreshed), 0 = held by someone else
local owner = redis.call('HGET', KEYS[1], 'owner')

if not owner then
  local fence = redis.call('INCR', KEYS[2])
  redis.call('HSET', KEYS[1],
    'owner', ARGV[1], 'service', ARGV[3], 'instance', ARGV[4], 'acquired', ARGV[5], 'fence', fence)
  redis.call('PEXPIRE', KEYS[1], ARGV[2])
  return { 1, fence, ARGV[1] }
end

if owner == ARGV[1] then
  -- Idempotent re-acquisition (e.g. a retried request carrying the same idempotency key, possibly on another
  -- instance): keep the original acquisition time and fencing token, adopt the new instance, refresh the lease.
  redis.call('HSET', KEYS[1], 'instance', ARGV[4], 'service', ARGV[3])
  redis.call('PEXPIRE', KEYS[1], ARGV[2])
  return { 2, tonumber(redis.call('HGET', KEYS[1], 'fence')), owner }
end

return { 0, redis.call('PTTL', KEYS[1]), owner }
