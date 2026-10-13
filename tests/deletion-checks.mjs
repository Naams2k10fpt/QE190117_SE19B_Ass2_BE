import assert from 'node:assert/strict';
import {randomUUID,randomBytes} from 'node:crypto';
import {execFileSync,spawn} from 'node:child_process';
const api=process.env.TEST_API_URL||'http://127.0.0.1:5196';
const db=new URL(process.env.DATABASE_URL);
const prefix='undo'+randomUUID().replaceAll('-','');
const password=randomBytes(24).toString('base64');
const pg=process.env.TEST_PSQL_PATH||'psql';
const ssl=process.env.TEST_DB_SSLMODE||'require';
if(ssl==='disable')assert.ok(['127.0.0.1','localhost'].includes(db.hostname),'Disable TLS only for an isolated loopback test DB.');
const env={...process.env,PGHOST:db.hostname,PGPORT:db.port||'5432',PGDATABASE:decodeURIComponent(db.pathname.slice(1)),PGUSER:decodeURIComponent(db.username),PGPASSWORD:decodeURIComponent(db.password),PGSSLMODE:ssl,PGCONNECT_TIMEOUT:'15',PGOPTIONS:'-c timezone=UTC'};
const args=['-X','-w','-t','-A','-v','ON_ERROR_STOP=1','-v','prefix='+prefix+'%'];
function sql(input){try{return execFileSync(pg,args,{env,input,encoding:'utf8',stdio:['pipe','pipe','pipe'],timeout:30000}).trim();}catch{throw Error('Scoped deletion test SQL failed; credentials/raw output withheld.');}}
function snapshot(){return [['Department','"DepartmentID"'],['Project','"ProjectID"'],['Task','"TaskID"'],['Tag','"TagID"'],['TaskTag','"TaskID","TagID"'],['SystemAccount','"AccountID"'],['RefreshToken','"RefreshTokenID"']].map(([table,order])=>sql(`SELECT md5(COALESCE(jsonb_agg(r)::text,'[]')) FROM (SELECT * FROM public."${table}" ORDER BY ${order}) r;`)).join('/');}
const baseline=snapshot();let checks=0;
function check(value,message){assert.ok(value,message);checks++;}
async function call(method,path,status,body,token){const res=await fetch(api+path,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},body:body===undefined?undefined:JSON.stringify(body),signal:AbortSignal.timeout(30000)});check((Array.isArray(status)?status:[status]).includes(res.status),method+' '+path+' unexpected '+res.status);return {status:res.status,data:res.status===204?null:res.headers.get('content-type')?.includes('json')?await res.json():null};}
async function register(suffix){return (await call('POST','/api/auth/register',201,{fullName:prefix+' '+suffix,email:prefix+suffix.toLowerCase()+'@example.test',password})).data;}
async function login(account){return (await call('POST','/api/auth/login',200,{email:account.email,password})).data;}
let adminToken,staffToken;
async function history(resource,entityId,token=staffToken){const page=(await call('GET','/api/deletions?resource='+resource+'&search='+prefix+'&pageSize=100',200,undefined,token)).data;const rows=page.items.filter(row=>row.entityId===entityId);check(rows.length>0,'Successful deletion must be recorded.');const item=rows[0];check(!('snapshot' in item)&&!('passwordHash' in item),'History must omit snapshots/hashes.');check(Date.parse(item.undoUntil)-Date.parse(item.deletedAt)===86400000,'Undo deadline is exactly 24 hours.');return item;}
const restore=(id,status=200,token=staffToken)=>call('POST','/api/deletions/'+id+'/restore',status,{undoUntil:'2099-01-01',restoredById:2147483647},token);
try{
 await call('GET','/api/deletions',401);await restore(1,401,undefined);
 const staff=await register('Staff'),admin=await register('Admin');
 sql(`UPDATE public."SystemAccount" SET "Role"=1 WHERE "AccountID"=${admin.accountId} AND "Email" LIKE :'prefix';`);
 adminToken=(await login(admin)).accessToken;staffToken=(await login(staff)).accessToken;
 await call('GET','/api/deletions?resource=accounts',403,undefined,staffToken);
 for(const query of ['resource=invalid','state=invalid','page=0','page=10001','pageSize=101'])await call('GET','/api/deletions?'+query,400,undefined,staffToken);
 await restore(2147483647,404);
 const d=(await call('POST','/api/departments',201,{departmentName:prefix+'_D',departmentDescription:'Undo test'},staffToken)).data;
 const p=(await call('POST','/api/projects',201,{projectName:prefix+'_P',startDate:'2026-10-13',status:0,departmentId:d.departmentId},staffToken)).data;
 const tag=(await call('POST','/api/tags',201,{tagName:prefix+'_Attached',color:'#123456'},staffToken)).data;
 const task=(await call('POST','/api/tasks',201,{title:prefix+'_Task',status:0,priority:0,projectId:p.projectId,tagIds:[tag.tagId]},staffToken)).data;
 await call('DELETE','/api/departments/'+d.departmentId,400,undefined,staffToken);
 await call('DELETE','/api/tasks/'+task.taskId,204,undefined,staffToken);
 await call('GET','/api/tasks/'+task.taskId,404);
 await call('DELETE','/api/accounts/'+staff.accountId,409,undefined,adminToken);
 await call('DELETE','/api/tags/'+tag.tagId,400,undefined,staffToken);
 const taskHistory=await history('tasks',task.taskId);await restore(taskHistory.deletionId,200,adminToken);
 const restoredTask=(await call('GET','/api/tasks/'+task.taskId,200)).data;
 check(restoredTask.createdById===staff.accountId&&restoredTask.updatedById===admin.accountId&&restoredTask.tags.length===1,'Task restore preserves creator/tags and audits actual restorer.');
 await restore(taskHistory.deletionId,409);
 check(sql(`SELECT count(*) FROM public."DeletionHistory" WHERE "EntityType"='departments' AND "EntityID"=${d.departmentId};`)==='0','Rejected deletion creates no history.');
 const pd=(await call('POST','/api/departments',201,{departmentName:prefix+'_Parent',departmentDescription:'Parent ordering'},staffToken)).data;
 const pp=(await call('POST','/api/projects',201,{projectName:prefix+'_Child',startDate:'2026-10-13',status:0,departmentId:pd.departmentId},staffToken)).data;
 await call('DELETE','/api/projects/'+pp.projectId,204,undefined,staffToken);const ph=await history('projects',pp.projectId);
 await call('DELETE','/api/departments/'+pd.departmentId,204,undefined,staffToken);const dh=await history('departments',pd.departmentId);
 await restore(ph.deletionId,409);await restore(dh.deletionId);await restore(ph.deletionId);
 check((await call('GET','/api/projects/'+pp.projectId,200)).data.departmentId===pd.departmentId,'Parent/child original IDs restored.');
 const recoverTag=(await call('POST','/api/tags',201,{tagName:prefix+'_Recover',color:'#ABCDEF'},staffToken)).data;
 await call('DELETE','/api/tags/'+recoverTag.tagId,204,undefined,staffToken);const th=await history('tags',recoverTag.tagId);
 const conflictTag=(await call('POST','/api/tags',201,{tagName:(prefix+'_Recover').toUpperCase(),color:'#123456'},staffToken)).data;
 await restore(th.deletionId,409);
 await call('DELETE','/api/tags/'+conflictTag.tagId,204,undefined,staffToken);await restore(th.deletionId);
 check((await call('GET','/api/tags',200)).data.find(t=>t.tagId===recoverTag.tagId)?.color==='#ABCDEF','Tag color/ID preserved, duplicate case-insensitive name never overwritten.');
 for(const suffix of ['Expired','Boundary']){
  const t=(await call('POST','/api/tags',201,{tagName:prefix+'_'+suffix,color:'#123456'},staffToken)).data;
  await call('DELETE','/api/tags/'+t.tagId,204,undefined,staffToken);const h=await history('tags',t.tagId);
  const elapsed=suffix==='Expired'?'25 hours':'24 hours';
  sql(`WITH t AS (SELECT clock_timestamp()-INTERVAL '${elapsed}' AS at) UPDATE public."DeletionHistory" h SET "DeletedAt"=t.at,"UndoUntil"=t.at+INTERVAL '24 hours' FROM t WHERE "DeletionID"=${h.deletionId} AND "EntityName" LIKE :'prefix';`);
  const result=await restore(h.deletionId,409);check(result.data.message.includes('24-hour'),'Expired/boundary restore rejected by server despite spoofed deadline.');
  check(sql(`SELECT count(*) FROM public."Tag" WHERE "TagID"=${t.tagId};`)==='0','Expired restore writes no entity.');
 }
 const race=(await call('POST','/api/tags',201,{tagName:prefix+'_Race',color:'#123456'},staffToken)).data;
 await call('DELETE','/api/tags/'+race.tagId,204,undefined,staffToken);const rh=await history('tags',race.tagId);
 const results=await Promise.all([restore(rh.deletionId,[200,409]),restore(rh.deletionId,[200,409],adminToken)]);
 check(results.filter(r=>r.status===200).length===1,'Concurrent restore has exactly one winner.');
 check(sql(`SELECT count(*) FROM public."Tag" WHERE "TagID"=${race.tagId};`)==='1','Concurrent restore inserts one original ID.');
 const waiting=(await call('POST','/api/tags',201,{tagName:prefix+'_LockExpiry',color:'#123456'},staffToken)).data;
 await call('DELETE','/api/tags/'+waiting.tagId,204,undefined,staffToken);const wh=await history('tags',waiting.tagId);
 sql(`WITH t AS (SELECT clock_timestamp() AS at) UPDATE public."DeletionHistory" h SET "DeletedAt"=t.at-INTERVAL '24 hours'+INTERVAL '2 seconds',"UndoUntil"=t.at+INTERVAL '2 seconds' FROM t WHERE "DeletionID"=${wh.deletionId} AND "EntityName" LIKE :'prefix';`);
 const hold=spawn(pg,args,{env,stdio:['pipe','pipe','pipe']});
 const held=new Promise((resolve,reject)=>{let out='';hold.stdout.on('data',b=>{out+=b;if(out.includes('LOCK_HELD'))resolve();});hold.on('error',()=>reject(Error('Test lock process failed.')));});
 const ended=new Promise((resolve,reject)=>hold.on('exit',code=>code===0?resolve():reject(Error('Test lock process failed.'))));
 hold.stdin.end(`BEGIN;SELECT "DeletionID" FROM public."DeletionHistory" WHERE "DeletionID"=${wh.deletionId} FOR UPDATE;SELECT 'LOCK_HELD';SELECT pg_sleep(4);COMMIT;`);
 await held;await restore(wh.deletionId,409);await ended;
 check(sql(`SELECT count(*) FROM public."Tag" WHERE "TagID"=${waiting.tagId};`)==='0','A request queued before expiry cannot restore after lock wait crosses deadline.');
 const account=await register('RecoverAccount');const old=await login(account);
 await call('DELETE','/api/accounts/'+account.accountId,204,undefined,adminToken);const ah=await history('accounts',account.accountId,adminToken);
 await restore(ah.deletionId,403,staffToken);await call('POST','/api/auth/login',401,{email:account.email,password});
 const replacement=(await call('POST','/api/auth/register',201,{fullName:prefix+' Replacement',email:account.email,password})).data;
 await restore(ah.deletionId,409,adminToken);await call('DELETE','/api/accounts/'+replacement.accountId,204,undefined,adminToken);
 await restore(ah.deletionId,200,adminToken);
 await call('GET','/api/auth/me',401,undefined,old.accessToken);await call('POST','/api/auth/refresh',401,{refreshToken:old.refreshToken});
 const recovered=await login(account);check(recovered.account.accountId===account.accountId&&recovered.account.role===0,'Account restores original ID, role and password, with fresh sessions only.');
 const elevated=await register('RecoverAdmin');sql(`UPDATE public."SystemAccount" SET "Role"=1 WHERE "AccountID"=${elevated.accountId} AND "Email" LIKE :'prefix';`);
 const priorAdmin=await login(elevated);await call('DELETE','/api/accounts/'+elevated.accountId,204,undefined,adminToken);
 const eh=await history('accounts',elevated.accountId,adminToken);await restore(eh.deletionId,200,adminToken);
 await call('GET','/api/accounts',401,undefined,priorAdmin.accessToken);
 const restoredAdmin=await login(elevated);check(restoredAdmin.account.role===1,'Account snapshot preserves prior Admin role.');await call('GET','/api/accounts',200,undefined,restoredAdmin.accessToken);
 const staffPage=(await call('GET','/api/deletions?search='+prefix,200,undefined,staffToken)).data;
 check(staffPage.items.every(i=>i.entityType!=='accounts'),'Staff all-resource history excludes every account entry.');
 const adminPage=(await call('GET','/api/deletions?search='+prefix+'&pageSize=100',200,undefined,adminToken)).data;
 check(!JSON.stringify(adminPage).match(/passwordHash|snapshot|accessToken|refreshToken/i),'Admin history contains no secrets or snapshot payload.');
 check(adminPage.items.some(i=>i.restoredAt&&i.restoredById===admin.accountId),'Restoration history records the actual authenticated actor.');
 await call('GET','/api/deletions?search='+prefix+'&state=expired',200,undefined,staffToken);
 const page=(await call('GET','/api/deletions?search='+prefix+'&pageSize=2',200,undefined,adminToken)).data;
 check(page.items.length===2&&page.totalItems>2,'History is paginated and reports total count.');
}finally{
 sql(`BEGIN;
 DELETE FROM public."TaskTag" WHERE "TaskID" IN (SELECT "TaskID" FROM public."Task" WHERE "CreatedByID" IN (SELECT "AccountID" FROM public."SystemAccount" WHERE "Email" LIKE :'prefix'));
 DELETE FROM public."Task" WHERE "CreatedByID" IN (SELECT "AccountID" FROM public."SystemAccount" WHERE "Email" LIKE :'prefix');
 DELETE FROM public."Tag" WHERE "TagName" ILIKE :'prefix';DELETE FROM public."Project" WHERE "ProjectName" LIKE :'prefix';
 DELETE FROM public."Department" WHERE "DepartmentName" LIKE :'prefix';DELETE FROM public."SystemAccount" WHERE "Email" LIKE :'prefix';
 DELETE FROM public."DeletionHistory" WHERE "EntityName" ILIKE :'prefix';COMMIT;`);
 check(snapshot()===baseline,'Original seven-table snapshot preserved after scoped cleanup.');
}
console.log(`PASS: ${checks} deletion/undo checks: all five resources, original IDs, parent/name conflicts, permissions, secret-free history, exact 24h expiry, lock-wait boundary, one concurrent winner, account session revocation and data preservation.`);
