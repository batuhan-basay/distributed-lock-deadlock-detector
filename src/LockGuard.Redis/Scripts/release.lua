-- Compare-and-delete. Used for normal release AND for forced release by the reclaimer / deadlock detector:
-- the expected owner must still own the key, so a lock that was meanwhile re-acquired by a healthy session is never stolen.
-- KEYS[1] = lock key   ARGV[1] = expected owner session id
-- Returns 1 if deleted, 0 otherwise.
if redis.call('HGET', KEYS[1], 'owner') == ARGV[1] then
  return redis.call('DEL', KEYS[1])
end
return 0
