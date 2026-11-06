-- Watchdog lease extension: only the current owner may extend.
-- KEYS[1] = lock key   ARGV[1] = session id   ARGV[2] = lease ttl in ms
-- Returns 1 if extended, 0 otherwise.
if redis.call('HGET', KEYS[1], 'owner') == ARGV[1] then
  return redis.call('PEXPIRE', KEYS[1], ARGV[2])
end
return 0
