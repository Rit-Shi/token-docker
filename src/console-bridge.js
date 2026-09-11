(() => {
  if (location.origin !== 'https://platform.deepseek.com' || window.top !== window) return;
  let authorization = '', busy = false, schedule;
  const clientHeaders = {Accept:'application/json','x-client-platform':'web'};
  const originalOpen = XMLHttpRequest.prototype.open;
  const originalHeader = XMLHttpRequest.prototype.setRequestHeader;
  const nativeFetch = window.fetch.bind(window);
  const report = (reason, area='usage') => chrome.webview.postMessage({type:'tokenbar-error',reason:area+': '+reason});
  const dateString = () => {const n=new Date();return `${n.getFullYear()}-${String(n.getMonth()+1).padStart(2,'0')}-${String(n.getDate()).padStart(2,'0')}`;};
  function body(json, endpoint) {
    if ((json.code != null && Number(json.code)!==0) || (json.data?.biz_code != null && Number(json.data.biz_code)!==0))
      throw Error(endpoint+' code='+String(json.code)+' biz='+String(json.data?.biz_code));
    if (!json.data?.biz_data) throw Error(endpoint+' missing biz_data');
    return json.data.biz_data;
  }
  function wallets(summary) {
    if (!Array.isArray(summary.normal_wallets)) throw Error('summary missing normal_wallets');
    return [...summary.normal_wallets,...(Array.isArray(summary.bonus_wallets)?summary.bonus_wallets:[])].map(w=>({currency:w.currency,amount:w.balance}));
  }
  function postBalance(summary) {
    chrome.webview.postMessage({type:'tokenbar-balance',date:dateString(),updatedAt:new Date().toISOString(),wallets:wallets(summary)});
  }
  XMLHttpRequest.prototype.open = function(method, url, ...args) {
    try {const target=new URL(url,location.href);this.__tbAllowed=target.origin===location.origin&&target.pathname.startsWith('/api/');this.__tbSummary=this.__tbAllowed&&target.pathname==='/api/v0/users/get_user_summary';}catch{this.__tbAllowed=false;}
    if (!this.__tbListener) {this.__tbListener=true;this.addEventListener('load',()=>{if(!this.__tbSummary||this.status!==200)return;try{const json=this.responseType==='json'?this.response:JSON.parse(this.responseText);postBalance(body(json,'summary'));}catch{}});}
    return originalOpen.call(this,method,url,...args);
  };
  XMLHttpRequest.prototype.setRequestHeader = function(name,value) {
    if(this.__tbAllowed&&value){const key=name.toLowerCase();if(key==='authorization'){authorization=value;clearTimeout(schedule);schedule=setTimeout(refresh,1800);}else if(/^x-(client|app)-/.test(key))clientHeaders[key]=value;}
    return originalHeader.call(this,name,value);
  };
  async function read(path) {
    const endpoint=path.split('?')[0];
    const controller=new AbortController();const timeout=setTimeout(()=>controller.abort(),20000);
    try{const response=await nativeFetch(path,{headers:{...clientHeaders,Authorization:authorization},credentials:'same-origin',signal:controller.signal});
      if(!response.ok)throw Error(endpoint+' HTTP '+response.status);
      return body(await response.json(),endpoint);
    }finally{clearTimeout(timeout);}
  }
  async function refresh() {
    if(busy)return;
    if(!authorization){report('等待控制台登录请求','auth');return;}
    busy=true;
    try {
      const now=new Date(),midnight=new Date(now.getFullYear(),now.getMonth(),now.getDate()),tomorrow=new Date(now.getFullYear(),now.getMonth(),now.getDate()+1);
      const start=+midnight/1000,end=+tomorrow/1000;
      const query=new URLSearchParams({start,end,tz:-now.getTimezoneOffset()*60});
      const summaryPromise=read('/api/v0/users/get_user_summary').then(summary=>{postBalance(summary);return wallets(summary);});
      const [amountResult,costResult,walletResult]=await Promise.allSettled([
        read('/api/v0/usage/by_api_key/amount?'+query),read('/api/v0/usage/by_api_key/cost?'+query),summaryPromise
      ]);
      if(walletResult.status==='rejected')report(walletResult.reason.message,'summary');
      if(amountResult.status==='rejected'||costResult.status==='rejected'){report((amountResult.status==='rejected'?amountResult.reason:costResult.reason).message);return;}
      if(walletResult.status==='rejected')return;
      const amount=amountResult.value,cost=costResult.value;
      if(!Array.isArray(amount.series)||!Array.isArray(cost.data))throw Error('usage schema: series/data missing');
      const inside=b=>Number(b.time)>=start&&Number(b.time)<end;
      const usage=amount.series.flatMap(s=>s.buckets.filter(inside).map(b=>({inputHit:b.usage.PROMPT_CACHE_HIT_TOKEN??'0',inputMiss:b.usage.PROMPT_CACHE_MISS_TOKEN??'0',output:b.usage.RESPONSE_TOKEN??'0'})));
      const costs=cost.data.flatMap(c=>c.series.flatMap(s=>s.buckets.filter(inside).map(b=>({currency:c.currency,amount:b.cost}))));
      chrome.webview.postMessage({type:'tokenbar-usage',date:dateString(),updatedAt:now.toISOString(),costs,usage,wallets:walletResult.value});
    }catch(error){report(error.message||'network error');}finally{busy=false;}
  }
  window.__tokenbarRefresh=refresh;
})();
