# Referenz-Prototyp der Log-Klassifizierung (Python)
#
# NUR REFERENZ. Nicht portieren, ohne docs/SPEC.md zu beachten.
# Bekannte Abweichungen zur Spezifikation (SPEC 5.9):
#   - ".json" allein gilt hier als Angriffsmuster -> Agent-Discovery wird faelschlich Angriff
#   - Browser-Anfragen mit 4xx werden als Bot gezaehlt
#   - Keine IP-Verifikation fuer KI-Agenten, kein Monitoring-Verdacht
#   - Kategorie-Reihenfolge weicht ab (Vite-Dev-Server steht in SPEC vor Secrets)
#
# Aufruf: python3 prototype_classifier.py <logdatei>  -> schreibt data.json
import re,json,collections,datetime,sys
F=sys.argv[1];C=collections.Counter
full=re.compile(r'^(\S+) - \S+ \[(\d\d/\w+/\d{4}):(\d\d):[^\]]*\] "([^"]*)" (\d{3}) (\d+) "([^"]*)" "([^"]*)" "([^"]*)"')
part=re.compile(r'^(10\.0\.1\.\d+) - - \[(\d\d/\w+/\d{4}):.*"(\d+\.\d+\.\d+\.\d+)"\s*$')
errl=re.compile(r'^\d{4}/\d\d/\d\d .*(\[error\]|<REDACTED>)')
R=[];errlines=0;unparsed=0
for l in open(F,errors='replace'):
    l=l.rstrip('\n')
    m=full.match(l)
    if m:
        px,d,h,req,st,sz,ref,ua,ip=m.groups()
        parts=req.split(' ')
        R.append(dict(d=d,h=int(h),m=parts[0] if parts else '',p=parts[1] if len(parts)>1 else '',s=int(st),ref=ref,ua=ua,ip=ip,px=px));continue
    m=part.match(l)
    if m:
        dom=re.search(r'@([\w.-]+)\)',l); R.append(dict(d=m.group(2),h=None,m='GET',p='?',s=None,ref='-',ua='(gekürzt) '+(dom.group(1) if dom else ''),ip=m.group(3),px=m.group(1)));continue
    if errl.match(l): errlines+=1; continue
    unparsed+=1
ATT=re.compile(r'\.php|wp-|wordpress|xmlrpc|\.env|\.git|\.svn|@fs|@vite|actuator|credentials|secret|\.aws|\.ssh|\.key\b|server\.key|sa\.json|key\.json|creds|\.json$|appsettings|settings\.json|trace\.axd|phpinfo|_ignition|telescope|_profiler|proc/self|/etc/|cgi-bin|cmd=|CMDI|GSCAN|rest_route|/admin|/vendor|/images/$|/webmail|/signin|/signup|/account|/form/|/api/|\.ya?ml|\.sql|\.bak|\.zip|\.tar|\.vscode|sftp|_environment|firebase|terraform|rootkey|\.config|/info$|/rest/|/backup|\.well-known/.+\.php|acme-challenge',re.I)
def cat(r):
    p=r['p']
    if r['m'] not in('GET','HEAD'): return 'POST-Proben & Login-Versuche'
    rules=[('WordPress',r'wp-|wordpress|xmlrpc|rest_route'),('Secrets & Credentials',r'\.env|credentials|\.aws|\.ssh|key|sa\.json|creds|appsettings|settings\.json|secret|terraform|rootkey|firebase|sftp|\.vscode|\.git|\.svn|\.ya?ml|\.config|_environment'),('Vite-Dev-Server',r'@fs|@vite'),('PHP-Webshells',r'\.php|phpinfo|_profiler'),('POST-Proben & Login-Versuche',r'/signin|/signup|/account'),('Framework-Endpunkte',r'actuator|_ignition|telescope|trace\.axd|/info$|/rest/'),('Path Traversal & Injection',r'\.\.|%2F|proc/self|/etc/|cmd=|CMDI|cgi-bin')]
    for n,r in rules:
        if re.search(r,p,re.I): return n
    return 'Sonstige Proben'
AI=[('ClaudeBot',r'ClaudeBot|anthropic\.com'),('Claude-User','Claude-User'),('Claude-SearchBot','Claude-SearchBot'),('GPTBot','GPTBot'),('OAI-SearchBot','OAI-SearchBot'),('ChatGPT-User','ChatGPT-User'),('PerplexityBot','PerplexityBot'),('Perplexity-User','Perplexity-User'),('Bytespider','Bytespider|bytedance'),('meta-externalagent','meta-externalagent'),('Applebot','Applebot'),('CCBot','CCBot')]
BOT=re.compile(r'bot|crawl|spider|slurp|preview|facebookexternalhit|censys|scan|curl|python|go-http|wget|headless|monitor|uptime|fetch|worker|\(gekürzt\)|^-$|^Mozilla/5.0$',re.I)
def ainame(ua):
    for n,r in AI:
        if re.search(r,ua,re.I): return n
def is_att(r):
    if r['s'] in(400,405): return True
    if r['m'] not in('GET','HEAD'): return True
    q=r['p']
    return r['s']==404 and bool(ATT.search(q)) or bool(re.search(r'CMDI|cmd=|command=|host=%60',q))
for r in R: r['att']=is_att(r)
scan=C(r['ip'] for r in R if r['att'])
scanners={ip for ip,n in scan.items() if n>=3}
for r in R:
    a=ainame(r['ua'])
    if r['att'] or r['ip'] in scanners: r['c']='attack'
    elif a: r['c']='ai'
    elif BOT.search(r['ua']): r['c']='bot'
    elif r['s'] and r['s']<400: r['c']='human'
    else: r['c']='bot'
    r['ai']=a
mon=dict(Aug=8,Sep=9)
def dkey(d): dd,mm,yy=d.split('/'); return f'{yy}-{mon[mm]:02d}-{dd}'
days=[]; start=datetime.date(2026,8,27)
while start<=datetime.date(2026,9,22): days.append(start.isoformat()); start+=datetime.timedelta(1)
daily={c:[0]*len(days) for c in['human','ai','bot','attack']}
for r in R: daily[r['c']][days.index(dkey(r['d']))]+=1
PAGE=re.compile(r'^/($|\?|[\w\-/]+\.html$|impressum$|download/)')
hum=[r for r in R if r['c']=='human']
pv=[r for r in hum if PAGE.match(r['p'])]
def clean(p):
    p=p.split('?')[0]; p=p[:-5] if p.endswith('.html') else p; return '/' if p in('/','/index') else p
top_pages=C(clean(r['p']) for r in pv).most_common(10)
def mask(ip): a=ip.split('.'); return '.'.join(a[:3])+'.x' if len(a)==4 else ip
hum_ips=C(mask(r['ip']) for r in pv)
refs=C()
for r in hum:
    f=r['ref']
    if f in('-',''):continue
    h=re.sub(r'^https?://','',f).split('/')[0].replace('www.','')
    if h=='guido-altmann.de':continue
    refs[h]+=1
att=[r for r in R if r['c']=='attack']
cats=C(cat(r) for r in att).most_common()
top_scan=[]
for ip,n in C(r['ip'] for r in att).most_common(10):
    rs=[r for r in att if r['ip']==ip]
    top_scan.append(dict(ip=ip,n=n,first=dkey(rs[0]['d']),focus=C(cat(r) for r in rs).most_common(1)[0][0],ua=C(r['ua'] for r in rs).most_common(1)[0][0][:48]))
aig=C(r['ai'] for r in R if r['ai'] and r['c']=='ai'); ais=C(r['ai'] for r in R if r['ai'] and r['c']=='attack')
ai_rows=sorted([dict(n=n,real=aig[n],fake=ais[n]) for n,_ in AI if aig[n] or ais[n]],key=lambda x:-(x['real']+x['fake']))
status=C(r['s'] for r in R if r['s']).most_common()
hours=[0]*24;hours_a=[0]*24
for r in R:
    if r['h'] is None:continue
    (hours if r['c']=='human' else hours_a if r['c']=='attack' else [0]*24)[r['h']]+=1
pdf=sum(1 for r in R if r['p'].endswith('.pdf') and r['s'] and r['s']<400 and r['c']=='human')
out=dict(period=[days[0],days[-1]],days=days,daily=daily,total=len(R),errlines=errlines,rawlines=sum(1 for _ in open(F,errors='replace')),
 classes={c:sum(v) for c,v in daily.items()},pageviews=len(pv),uniq_visitors=len(hum_ips),top_pages=top_pages,refs=refs.most_common(8),
 cats=cats,top_scan=top_scan,scanners=len(scanners),ai=ai_rows,status=status,hours=hours,hours_att=hours_a,pdf=pdf,
 s5xx=sum(1 for r in R if r['s'] and r['s']>=500),sitemap=sum(1 for r in R if r['p']=='/sitemap.xml'),robots=sum(1 for r in R if r['p']=='/robots.txt'),
 llms=sum(1 for r in R if r['p']=='/llms.txt'),wellknown=sorted({r['p'] for r in R if r['p'].startswith('/.well-known/') and r['p'].endswith('.json')}),
 proxies=[(px,dkey([r for r in R if r['px']==px][0]['d']),dkey([r for r in R if r['px']==px][-1]['d'])) for px in dict.fromkeys(r['px'] for r in R)],
 top_ips_h=hum_ips.most_common(6))
json.dump(out,open('data.json','w'),ensure_ascii=False)
print(json.dumps({k:out[k] for k in['total','errlines','rawlines','classes','pageviews','uniq_visitors','top_pages','refs','cats','scanners','ai','status','pdf','proxies','top_ips_h','llms','wellknown']},ensure_ascii=False,indent=0)[:4000])
for t in top_scan: print(t)
