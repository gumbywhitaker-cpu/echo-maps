window.__build = function (MODS, PACK, VER, DESC) {
  // code mask: 1 = real code char (not inside a string, char literal or comment)
  function codeMask(s) {
    const n = s.length, m = new Uint8Array(n), st = []; let i = 0;
    while (i < n) {
      const top = st[st.length - 1], c = s[i], d = s[i + 1];
      if (!top || top.t === 'hole') {
        if (c === '/' && d === '/') { while (i < n && s[i] !== '\n') i++; continue; }
        if (c === '/' && d === '*') { i += 2; while (i < n && !(s[i] === '*' && s[i + 1] === '/')) i++; i += 2; continue; }
        if (top && top.t === 'hole') { if (c === '{') top.d++; else if (c === '}') { if (top.d === 0) { st.pop(); i++; continue; } top.d--; } }
        let j = i, interp = false, verb = false;
        while (s[j] === '$' || s[j] === '@') { if (s[j] === '$') interp = true; else verb = true; j++; }
        if (s[j] === '"') {
          if (interp) { st.push({ t: 'istr', verb }); i = j + 1; continue; }
          i = j + 1;
          while (i < n) {
            if (verb) { if (s[i] === '"') { if (s[i + 1] === '"') { i += 2; continue; } i++; break; } }
            else { if (s[i] === '\\') { i += 2; continue; } if (s[i] === '"') { i++; break; } if (s[i] === '\n') break; }
            i++;
          }
          continue;
        }
        if (c === "'") { i++; if (s[i] === '\\') i += 2; else i++; while (i < n && s[i] !== "'") i++; i++; continue; }
        m[i] = 1; i++; continue;
      } else {
        if (!top.verb && c === '\\') { i += 2; continue; }
        if (top.verb && c === '"' && d === '"') { i += 2; continue; }
        if (c === '"') { st.pop(); i++; continue; }
        if (c === '{') { if (d === '{') { i += 2; continue; } st.push({ t: 'hole', d: 0 }); i++; continue; }
        if (c === '}' && d === '}') { i += 2; continue; }
        i++; continue;
      }
    }
    return m;
  }
  function depths(s) { const m = codeMask(s), dp = new Int16Array(s.length + 1); let d = 0; for (let i = 0; i < s.length; i++) { dp[i] = d; if (m[i]) { if (s[i] === '{') d++; else if (s[i] === '}') d--; } } dp[s.length] = d; return dp; }
  function bodyOf(src, name) {
    const re = new RegExp('public\\s+(?:partial\\s+)?class\\s+' + name + '\\s*:\\s*RustPlugin\\s*\\{'); const mt = re.exec(src);
    if (!mt) throw new Error('class not found: ' + name);
    const msk = codeMask(src); let start = mt.index + mt[0].length, depth = 1, i = start;
    while (depth && i < src.length) { if (msk[i]) { if (src[i] === '{') depth++; else if (src[i] === '}') depth--; } i++; }
    return src.slice(start, i - 1);
  }
  const argnames = p => !p.trim() ? '' : p.split(',').map(x => x.split('=')[0].trim().split(/\s+/).pop()).join(', ');
  const usings = new Set(), bodies = [], hooks = {}, ccmds = [], apis = [], cfgMods = [], notes = [];
  for (const name of MODS) {
    const src = window.__all[name];
    (src.match(/^using [^;=]+;/gm) || []).forEach(u => usings.add(u.trim()));
    let b = bodyOf(src, name);
    b = b.replace(/(?<![\w.])Chat\.ChatChannel/g, 'ConVar.Chat.ChatChannel');
    b = b.replace(/\[PluginReference\]\s*private\s+Plugin\s+([^;]+);/g, (m, g) => g.split(',').map(x => x.trim()).map(x => `private Plugin ${x} => EP.Find("${x}");`).join(' '));
    b = b.replace(/cmd\.AddChatCommand\(("[^"]+"),\s*this,\s*nameof\((\w+)\)\)/g, 'EP.AddChat($1, $2)');
    b = b.replace(/,\s*this\)/g, ', EP)');
    b = b.replace(/protected\s+override\s+void\s+(LoadDefaultConfig|LoadConfig|SaveConfig)\s*\(\s*\)/g, 'internal override void $1()');
    if (/internal override void (LoadDefaultConfig|LoadConfig|SaveConfig)\(\)|\bConfig\.(ReadObject|WriteObject|Get|Set|Save)/.test(b)) cfgMods.push(name);
    let dp = depths(b);
    // console commands (module level only)
    const ccRe = /\[ConsoleCommand\(("[^"]+")\)\]\s*(?:private |internal |public )?void\s+(\w+)\s*\(\s*ConsoleSystem\.Arg\s+(\w+)\s*\)/g; let cm;
    while ((cm = ccRe.exec(b))) if (dp[cm.index] === 0) ccmds.push([cm[1], name, cm[2]]);
    b = b.replace(/\[ConsoleCommand\("[^"]+"\)\]\s*/g, '');
    if (/\[ChatCommand\(/.test(b)) notes.push(name + ': has [ChatCommand] attributes - NOT handled');
    b = b.replace(/^(\s*)private /gm, '$1internal ');
    b = b.replace(/(?<=[\s;{])private (?=(?:static |readonly |const )*[\w<>\[\], .?]+\s+\w+\s*[=;({])/g, 'internal ');
    dp = depths(b);
    const hookRe = /^\s*(?:internal |public |protected )?(?:static )?(object|void|bool\??)\s+((?:On|Can)[A-Z]\w*|Init|Unload|Loaded)\s*\(([^)]*)\)/gm; let hm;
    while ((hm = hookRe.exec(b))) { const at = hm.index + hm[0].search(/\S/); if (dp[at] !== 0) continue; (hooks[hm[2]] = hooks[hm[2]] || []).push([name, hm[1], hm[3].trim()]); }
    const apiRe = /^\s*(?:internal |public |protected )?(?:static )?([\w<>\[\], .?]+?)\s+(API_\w+)\s*\(([^)]*)\)/gm; let am;
    while ((am = apiRe.exec(b))) { const at = am.index + am[0].search(/\S/); if (dp[at] !== 0) continue; apis.push([name, am[1].trim(), am[2], am[3].trim()]); }
    bodies.push(`    // ===================== ${name} =====================\n    internal class M_${name} : EchoMod\n    {\n        internal override string ModName => "${name}";\n${b}\n    }\n`);
  }
  // make sure every config module gets LoadConfig before Init
  if (cfgMods.length && !hooks.Init) hooks.Init = [];
  const main = [];
  for (const [hn, lst] of Object.entries(hooks)) {
    let params = lst.reduce((a, x) => x[2].length > a.length ? x[2] : a, '');
    const nonVoid = lst.some(x => x[1] !== 'void');
    const ret = !nonVoid ? 'void' : 'object';
    const mnames = params ? params.split(',').map(x => x.split('=')[0].trim().split(/\s+/).pop()) : [];
    const mtypes = params ? params.split(',').map(x => x.trim().split(/\s+/)[0]) : [];
    const lines = [];
    if (hn === 'Init') for (const cmn of cfgMods) lines.push(`        Safe("${cmn}", "LoadConfig", () => m_${cmn}.LoadConfig());`);
    for (const [name, r, p] of lst) {
      let call, guard = 'true';
      if (p && p !== params) {
        const pt = p.split(',').map(x => x.trim().split(/\s+/)[0]); const conv = [];
        pt.forEach((t, i) => { conv.push(t === mtypes[i] ? mnames[i] : `(${mnames[i]} as ${t})`); if (t !== mtypes[i]) guard += ` && ${mnames[i]} is ${t}`; });
        call = `m_${name}.${hn}(${conv.join(', ')})`;
      } else call = `m_${name}.${hn}(${p ? argnames(p) : ''})`;
      const sub = `!m_${name}.Unsub.Contains("${hn}")`;
      if (r !== 'void' && ret === 'object') lines.push(`        if (${sub} && ${guard}) { var r_${name} = SafeO("${name}", "${hn}", () => (object)${call}); if (r_${name} != null) return r_${name}; }`);
      else lines.push(`        if (${sub} && ${guard}) Safe("${name}", "${hn}", () => ${call});`);
    }
    main.push(`    private ${ret} ${hn}(${params})\n    {\n${lines.join('\n')}\n${ret === 'object' ? '        return null;\n' : ''}    }\n`);
  }
  const apiOut = apis.map(([name, rt, an, p]) => `    private ${rt} ${an}(${p}) => m_${name}.${an}(${argnames(p)});`);
  const ccs = ccmds.map(([c, mod, meth]) => `    [ConsoleCommand(${c})] private void cc_${mod}_${meth}(ConsoleSystem.Arg a) => m_${mod}.${meth}(a);`);
  ['using System;', 'using System.Collections.Generic;', 'using Oxide.Core;', 'using Oxide.Core.Plugins;'].forEach(u => usings.add(u));
  ['using ConVar;', 'using Oxide.Core.Libraries;', 'using Oxide.Plugins;'].forEach(u => usings.delete(u));
  const fields = MODS.map(n => `    private readonly M_${n} m_${n} = new M_${n}();`).join('\n');
  const binds = MODS.map(n => `        m_${n}.EP = this;`).join('\n');
  const out = `// ${PACK} ${VER} (Nick, ${new Date().toDateString()}): ${MODS.length} small Echo plugins folded into one. Each one's code is unchanged and
//   runs as its own module below; config files, data files, chat and console commands are exactly the same as before:
//   ${MODS.join(', ')}
${[...usings].sort().join('\n')}

namespace Oxide.Plugins
{
    [Info("${PACK}", "Echo 10X", "${VER}")]
    [Description("${DESC}")]
    public class ${PACK} : RustPlugin
    {
${fields}

    public ${PACK}()
    {
${binds}
    }

    // ---- shared plumbing for the modules ----
    internal Plugin Find(string n) => plugins.Find(n);
    internal void AddChat(string c, Action<BasePlayer, string, string[]> cb) => cmd.AddChatCommand(c, this, cb);
    internal PluginTimers Timers => timer;
    internal Oxide.Core.Libraries.Permission Perm => permission;
    internal void Log(string mod, string m) => Puts("[" + mod + "] " + m);
    internal void Warn(string mod, string m) => PrintWarning("[" + mod + "] " + m);
    internal void Err(string mod, string m) => PrintError("[" + mod + "] " + m);
    internal void NT(Action a) => NextTick(a);
    internal void ChatAll(string m) => PrintToChat(m);
    internal void Reply(BasePlayer p, string m) => SendReply(p, m);
    internal void Reply(ConsoleSystem.Arg a, string m) => SendReply(a, m);
    private void Safe(string mod, string hook, Action a) { try { a(); } catch (Exception e) { PrintError("[" + mod + "] " + hook + ": " + e); } }
    private object SafeO(string mod, string hook, Func<object> f) { try { return f(); } catch (Exception e) { PrintError("[" + mod + "] " + hook + ": " + e); return null; } }

    internal abstract class EchoMod
    {
        internal ${PACK} EP;
        internal abstract string ModName { get; }
        internal readonly HashSet<string> Unsub = new HashSet<string>();
        internal void Subscribe(string h) => Unsub.Remove(h);
        internal void Unsubscribe(string h) => Unsub.Add(h);
        internal PluginTimers timer => EP.Timers;
        internal Oxide.Core.Libraries.Permission permission => EP.Perm;
        internal string Name => ModName;
        internal string Title => ModName;
        internal void Puts(string m) => EP.Log(ModName, m);
        internal void Puts(string f, params object[] a) => EP.Log(ModName, string.Format(f, a));
        internal void PrintWarning(string m) => EP.Warn(ModName, m);
        internal void PrintWarning(string f, params object[] a) => EP.Warn(ModName, string.Format(f, a));
        internal void PrintError(string m) => EP.Err(ModName, m);
        internal void PrintToChat(string m) => EP.ChatAll(m);
        internal void SendReply(BasePlayer p, string m) => EP.Reply(p, m);
        internal void SendReply(ConsoleSystem.Arg a, string m) => EP.Reply(a, m);
        internal void NextTick(Action a) => EP.NT(a);
        // same log file as when it was its own plugin: oxide/logs/<Name>/<name>_<file>-<date>.txt
        internal void LogToFile(string f, string m, Plugin p, bool dated = true, bool stamp = false)
        {
            try
            {
                var dir = System.IO.Path.Combine(Interface.Oxide.LogDirectory, ModName);
                System.IO.Directory.CreateDirectory(dir);
                var file = ModName.ToLower() + "_" + f.ToLower() + (dated ? "-" + DateTime.Now.ToString("yyyy-MM-dd") : "") + ".txt";
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, file), (stamp ? "[" + DateTime.Now.ToString("HH:mm:ss") + "] " : "") + m + Environment.NewLine);
            }
            catch { }
        }
        // config: same file as before, oxide/config/<Name>.json
        internal Oxide.Core.Configuration.DynamicConfigFile Config;
        internal virtual void LoadDefaultConfig() { }
        internal virtual void SaveConfig() { Config?.Save(); }
        internal virtual void LoadConfig()
        {
            Config = new Oxide.Core.Configuration.DynamicConfigFile(System.IO.Path.Combine(Interface.Oxide.ConfigDirectory, ModName + ".json"));
            if (Config.Exists()) { try { Config.Load(); } catch (Exception e) { PrintError("Config file unreadable: " + e.Message); } }
            else { LoadDefaultConfig(); SaveConfig(); }
        }
    }

    // ---- hooks: each one is handed to every module that uses it ----
${main.join('')}
    // ---- APIs other plugins call (same names as before) ----
${apiOut.join('\n')}

    // ---- console commands (same names as before) ----
${ccs.join('\n')}

${bodies.join('')}
    }
}
`;
  return { out, hooks: Object.fromEntries(Object.entries(hooks).map(([k, v]) => [k, v.length])), ccmds: ccmds.length, apis: apis.map(a => a[0] + '.' + a[2]), cfgMods, notes };
};
'ok';
