#!/bin/bash
# End-to-end check of auth, roles and ownership guards. Needs the API running, plus curl, jq and psql.
# Registers throwaway users (authtest-*@example.test) and deletes them again at the end.
API=${API:-http://localhost:5087}
PSQL="psql -h localhost -U postgres -d armafit -At"
export PGPASSWORD=postgres
RUN=$RANDOM
OUT=$(mktemp)
fail=0

# req METHOD PATH [TOKEN] [JSON] -> sets $code and $body
req() {
  local args=(-s -o $OUT -w '%{http_code}' -X "$1" "$API$2" -H 'Content-Type: application/json')
  [ -n "$3" ] && args+=(-H "Authorization: Bearer $3")
  [ -n "$4" ] && args+=(-d "$4")
  code=$(curl "${args[@]}")
  body=$(cat $OUT)
}
expect() { # expect CODE LABEL
  if [ "$code" = "$1" ]; then echo "ok   $code  $2"; else echo "FAIL $code (wanted $1)  $2  -> $body"; fail=1; fi
}
check() { # check LABEL CONDITION...
  local label=$1; shift
  if "$@"; then echo "ok        $label"; else echo "FAIL      $label"; fail=1; fi
}
register() { # register NAME ROLE -> echoes id
  req POST /api/auth/register "" "{\"email\":\"authtest-$RUN-$1@example.test\",\"password\":\"Secret123!\",\"fullName\":\"Test $1\",\"role\":\"$2\"}"
  echo "$body" | jq -r .id
}
login() { # login NAME -> sets $access $refresh
  req POST /api/auth/login "" "{\"email\":\"authtest-$RUN-$1@example.test\",\"password\":\"Secret123!\"}"
  access=$(echo "$body" | jq -r .accessToken); refresh=$(echo "$body" | jq -r .refreshToken)
}

A_ID=$(register a athlete); B_ID=$(register b athlete); T_ID=$(register t trainer); T2_ID=$(register t2 trainer)
echo "users: A=$A_ID B=$B_ID T=$T_ID T2=$T2_ID"

echo "--- authentication"
req GET /api/plans;                       expect 401 "no token"
check "401 body is problem+json" grep -q '"status":401' $OUT
req GET /api/exercises "" ;               expect 401 "exercises need a login too"
req GET /openapi/v1.json;                 expect 200 "openapi document stays public"
check "openapi has Bearer scheme" [ "$(jq -r '.components.securitySchemes.Bearer.scheme' $OUT)" = "bearer" ]
check "openapi documents 401 on plans" [ "$(jq -r '.paths["/api/plans"].get.responses["401"] | has("content")' $OUT)" = "true" ]
req POST /api/auth/login "" "{\"email\":\"authtest-$RUN-a@example.test\",\"password\":\"wrong\"}"; expect 401 "wrong password"

login a;  A=$access; A_REFRESH=$refresh
payload=$(echo "$A" | cut -d. -f2 | tr '_-' '/+'); while [ $(( ${#payload} % 4 )) -ne 0 ]; do payload="$payload="; done
claims=$(echo "$payload" | base64 -d)
echo "claims: $claims"
check "token sub = user id"   [ "$(echo "$claims" | jq -r .sub)" = "$A_ID" ]
check "token role = Athlete"  [ "$(echo "$claims" | jq -r .role)" = "Athlete" ]
check "token has sid"         [ -n "$(echo "$claims" | jq -r '.sid // empty')" ]
login b;  B=$access
login t;  T=$access
login t2; T2=$access
req GET /api/exercises "$A";              expect 200 "exercises with token"
EX_ID=$(echo "$body" | jq -r '.[0].id')
req GET /api/plans "${A}x";               expect 401 "tampered signature"

echo "--- plans: ownership"
req POST /api/plans "$A" "{\"name\":\"A plan\",\"athleteId\":$A_ID}";   expect 201 "athlete creates own plan"
PLAN=$(echo "$body" | jq -r .id)
check "createdBy comes from the token" [ "$(echo "$body" | jq -r .createdBy)" = "$A_ID" ]
req POST /api/plans "$A" "{\"name\":\"x\",\"athleteId\":$B_ID}";        expect 403 "athlete cannot create a plan for another athlete"
req GET /api/plans "$A";                  expect 200 "athlete lists plans"
check "A sees exactly own plan" [ "$(echo "$body" | jq -c '[.items[].id]')" = "[$PLAN]" ]
req GET /api/plans "$B";                  check "B sees no plans" [ "$(echo "$body" | jq -r .totalCount)" = "0" ]
req GET "/api/plans?athleteId=$A_ID" "$B"; check "B filtering by A's id still sees nothing" [ "$(echo "$body" | jq -r .totalCount)" = "0" ]
req GET /api/plans/$PLAN "$B";            expect 403 "other athlete reads plan"
req PUT /api/plans/$PLAN "$B" '{"name":"hacked"}';                      expect 403 "other athlete updates plan"
req DELETE /api/plans/$PLAN "$B";         expect 403 "other athlete deletes plan"
req GET /api/plans/$PLAN/workouts "$B";   expect 403 "other athlete lists workouts"
req POST /api/plans/$PLAN/workouts "$B" '{"name":"W","dayNumber":1}';   expect 403 "other athlete adds workout"
req POST /api/plans/$PLAN/workouts "$B" '{}';                           expect 403 "403 wins over 400 for an invalid body"
req GET /api/plans/$PLAN "$T";            expect 403 "unlinked trainer reads plan"
req GET /api/plans/99999999 "$A";         expect 404 "unknown plan"
req GET /api/plans/99999999/workouts "$A"; expect 404 "unknown plan (nested)"

echo "--- invitations: roles"
req POST /api/invitations "$T" "{\"trainerEmail\":\"authtest-$RUN-t2@example.test\"}"; expect 403 "trainer cannot send invitation"
req POST /api/invitations "$A" "{\"trainerEmail\":\"authtest-$RUN-t@example.test\"}";  expect 201 "athlete invites trainer"
INV=$(echo "$body" | jq -r .id)
check "invitation athlete = caller" [ "$(echo "$body" | jq -r .athleteId)" = "$A_ID" ]
check "athlete is not offered accept" [ "$(echo "$body" | jq -r '._links.accept // "none"')" = "none" ]
req POST /api/invitations/$INV/accept "$A";  expect 403 "athlete cannot accept"
req POST /api/invitations/$INV/accept "$T2"; expect 403 "another trainer cannot accept"
req GET /api/invitations "$T2";           check "T2 sees no invitations" [ "$(echo "$body" | jq -r .totalCount)" = "0" ]
req GET /api/invitations "$B";            check "B sees no invitations" [ "$(echo "$body" | jq -r .totalCount)" = "0" ]
req GET /api/invitations "$T";            check "T sees it with an accept link" [ "$(echo "$body" | jq -r '.items[0]._links.accept.method')" = "POST" ]
req GET /api/plans/$PLAN "$T";            expect 403 "pending link gives no access"
req POST /api/invitations/$INV/accept "$T";  expect 200 "invited trainer accepts"

echo "--- trainer with an active link"
req GET /api/plans/$PLAN "$T";            expect 200 "trainer reads athlete's plan"
req GET /api/plans "$T";                  check "trainer lists athlete's plan" [ "$(echo "$body" | jq -c '[.items[].id]')" = "[$PLAN]" ]
req POST /api/plans/$PLAN/workouts "$T" '{"name":"Push","dayNumber":1}'; expect 201 "trainer adds workout"
WORKOUT=$(echo "$body" | jq -r .id)
req POST /api/plans/$PLAN/workouts/$WORKOUT/exercises "$T" "{\"exerciseId\":$EX_ID,\"sets\":3,\"reps\":8}"; expect 201 "trainer adds exercise"
req GET /api/plans/$PLAN/workouts/$WORKOUT/exercises "$B";  expect 403 "other athlete reads exercises"
req POST /api/plans "$T" "{\"name\":\"T plan\",\"athleteId\":$A_ID}";    expect 201 "trainer creates plan for own athlete"
check "createdBy = trainer" [ "$(echo "$body" | jq -r .createdBy)" = "$T_ID" ]
req POST /api/plans "$T" "{\"name\":\"x\",\"athleteId\":$B_ID}";         expect 403 "trainer cannot create plan for unlinked athlete"
req GET /api/plans/$PLAN "$T2";           expect 403 "other trainer still locked out"

echo "--- workout logs and progress"
LOG="{\"workoutId\":$WORKOUT,\"sets\":[{\"exerciseId\":$EX_ID,\"setNumber\":1,\"weightKg\":50,\"reps\":8}]}"
req POST /api/workout-logs "$T" "$LOG";   expect 403 "trainer cannot log a workout"
req POST /api/workout-logs "$B" "$LOG";   expect 403 "athlete cannot log another athlete's workout"
req POST /api/workout-logs "$A" "$LOG";   expect 201 "athlete logs own workout"
check "log user = caller" [ "$(echo "$body" | jq -r .userId)" = "$A_ID" ]
req GET /api/workout-logs "$A";           check "athlete sees own log" [ "$(echo "$body" | jq -r .totalCount)" = "1" ]
req GET "/api/workout-logs?athleteId=$A_ID" "$T";  expect 200 "trainer lists athlete's logs"
check "trainer sees the athlete's log with sets" [ "$(echo "$body" | jq -r '.items[0].sets[0].weightKg == 50 and .items[0].userId == '$A_ID)" = "true" ]
req GET "/api/workout-logs?athleteId=$A_ID" "$T2"; check "unlinked trainer sees no logs" [ "$(echo "$body" | jq -r .totalCount)" = "0" ]
req GET /api/workout-logs "$B";           check "other athlete sees no logs" [ "$(echo "$body" | jq -r .totalCount)" = "0" ]
req GET /api/athletes/$A_ID/progress "$A";  expect 200 "athlete reads own progress"
req GET /api/athletes/$A_ID/progress "$T";  expect 200 "trainer reads athlete's progress"
req GET /api/athletes/$A_ID/progress "$T2"; expect 403 "unlinked trainer reads progress"
req GET /api/athletes/$A_ID/progress "$B";  expect 403 "other athlete reads progress"

echo "--- refresh and logout"
req POST /api/auth/refresh "" "{\"refreshToken\":\"$A_REFRESH\"}";      expect 200 "refresh"
A2=$(echo "$body" | jq -r .accessToken); A2_REFRESH=$(echo "$body" | jq -r .refreshToken)
check "refresh token was rotated" [ "$A2_REFRESH" != "$A_REFRESH" ]
req POST /api/auth/refresh "" "{\"refreshToken\":\"$A_REFRESH\"}";      expect 401 "old refresh token is dead"
req POST /api/auth/refresh "" '{"refreshToken":"nonsense"}';            expect 401 "unknown refresh token"
req GET /api/plans "$A2";                 expect 200 "new access token works"
check "refresh tokens are stored hashed" [ "$($PSQL -c "select count(*) from sessions where refresh_token_hash = '$A2_REFRESH'")" = "0" ]
req POST /api/auth/logout "" "{\"refreshToken\":\"$A2_REFRESH\"}";      expect 204 "logout"
req GET /api/plans "$A2";                 expect 401 "access token dead right after logout"
req GET /api/plans "$A";                  expect 401 "earlier access token of that session dead too"
req POST /api/auth/refresh "" "{\"refreshToken\":\"$A2_REFRESH\"}";     expect 401 "refresh token dead after logout"
check "session marked revoked in the database" [ "$($PSQL -c "select count(*) from sessions where user_id = $A_ID and revoked_at is not null")" = "1" ]
req GET /api/plans "$B";                  expect 200 "other users' sessions unaffected"
$PSQL -c "update users set is_active = false where id = $B_ID" >/dev/null
req GET /api/plans "$B";                  expect 401 "deactivated user's token is rejected"

echo "--- cleanup"
$PSQL -c "delete from users where email like 'authtest-$RUN-%@example.test'"
rm -f $OUT
[ $fail = 0 ] && echo "ALL PASSED" || echo "SOME CHECKS FAILED"
exit $fail
