using System.Text.Json;

namespace Assimalign.Cohesion.Viu.Server;

internal static class BrowserRefreshClientScript
{
    private const string Template = """
        setTimeout(async function () {
          const sentinel = '_dotnet_watch_ws_injected';
          if (window.hasOwnProperty(sentinel)) return;
          window[sentinel] = true;

          const urls = '{{ENDPOINTS}}'.split(',');
          const serverKey = '{{SERVER_KEY}}';
          const virtualDirectory = '{{VIRTUAL_DIRECTORY}}'.replace(/^\/+|\/+$/g, '');
          const sharedSecret = await createSecret(serverKey);
          let socket;
          for (const url of urls) {
            try { socket = await connect(url, sharedSecret); break; }
            catch (error) { console.debug(error); }
          }
          if (!socket) {
            console.debug('Unable to establish a connection to the browser refresh server.');
            return;
          }

          socket.onmessage = async function (message) {
            const payload = JSON.parse(message.data);
            switch (payload.type) {
              case 'Reload': location.reload(); break;
              case 'Wait': waitForRebuild(); break;
              case 'UpdateStaticFile': await updateStaticFile(payload.path); break;
              case 'ApplyManagedCodeUpdates': await applyManagedCodeUpdates(payload); break;
              default: console.debug('Unsupported browser-refresh message:', payload.type); break;
            }
          };

          function waitForRebuild() {
            if (window._dotnet_watch_waiting) return;
            window._dotnet_watch_waiting = true;
            const title = document.title;
            const glyphs = ['☱', '☲', '☴'];
            let index = 0;
            setInterval(() => document.title = glyphs[index++ % glyphs.length] + ' ' + title, 240);
          }

          async function updateStaticFile(path) {
            if (!path || !path.endsWith('.css')) { location.reload(); return; }
            // The stylesheet branch must never reload: the Viu run-host bridge replays one
            // UpdateStaticFile per tracked stylesheet on every connection, so a reload here
            // reconnects and replays forever. This matches the dotnet-watch client contract:
            // swap the matching links in place and treat an unreferenced stylesheet as a no-op.
            const links = [...document.querySelectorAll('link[rel="stylesheet"]')]
              .filter(link => link.href.includes(path));
            if (links.length === 0) {
              console.debug('No stylesheet link matches ' + path + '; ignoring the update.');
              return;
            }
            try { await fetch(applicationPath('/_framework/clear-browser-cache'), { cache: 'no-store' }); }
            catch (error) { console.debug(error); }
            links.forEach(link => {
              const replacement = link.cloneNode();
              replacement.href = link.href.split('?', 1)[0] + '?nonce=' + Date.now();
              replacement.addEventListener('load', () => link.remove());
              link.parentNode.insertBefore(replacement, link.nextSibling);
            });
          }

          async function applyManagedCodeUpdates(payload) {
            if (sharedSecret && payload.sharedSecret !== sharedSecret.encoded) {
              socket.send(JSON.stringify({ success: false, log: [{ severity: 2, message: 'Browser refresh server validation failed.' }] }));
              return;
            }
            let error;
            try {
              const apply = window.Blazor?._internal?.applyHotReloadDeltas;
              if (apply) await apply(payload.deltas, payload.responseLoggingLevel);
              else {
                const legacy = window.Blazor?._internal?.applyHotReload;
                if (legacy) for (const delta of payload.deltas) await legacy(delta.moduleId, delta.metadataDelta, delta.ilDelta, delta.pdbDelta, delta.updatedTypes);
              }
              await fetch(applicationPath('/_framework/blazor-hotreload'), {
                method: 'POST',
                headers: { 'content-type': 'application/json' },
                body: JSON.stringify({ id: payload.updateId, deltas: payload.deltas })
              });
            } catch (caught) { error = caught; }
            socket.send(JSON.stringify({ success: !error, log: error ? [{ severity: 2, message: String(error) }] : [] }));
          }

          function applicationPath(path) {
            return virtualDirectory ? '/' + virtualDirectory + path : path;
          }

          async function createSecret(key) {
            if (!key || !window.crypto?.subtle) return null;
            const bytes = window.crypto.getRandomValues(new Uint8Array(32));
            const binary = Uint8Array.from(atob(key), character => character.charCodeAt(0));
            const publicKey = await window.crypto.subtle.importKey('spki', binary, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['encrypt']);
            const encrypted = await window.crypto.subtle.encrypt({ name: 'RSA-OAEP' }, publicKey, bytes);
            return {
              encrypted: btoa(String.fromCharCode(...new Uint8Array(encrypted))),
              encoded: btoa(String.fromCharCode(...bytes))
            };
          }

          function connect(url, secret) {
            return new Promise((resolve, reject) => {
              const protocol = secret ? encodeURIComponent(secret.encrypted) : [];
              const candidate = new WebSocket(url, protocol);
              let opened = false;
              candidate.addEventListener('open', () => { opened = true; resolve(candidate); }, { once: true });
              candidate.addEventListener('close', event => { if (!opened) reject(event); }, { once: true });
            });
          }
        }, 500);
        """;

    public static string Create(BrowserRefreshConfiguration configuration)
    {
        return Template
            .Replace("{{ENDPOINTS}}", Encode(configuration.WebSocketEndpoints), System.StringComparison.Ordinal)
            .Replace("{{SERVER_KEY}}", Encode(configuration.ServerKey), System.StringComparison.Ordinal)
            .Replace("{{VIRTUAL_DIRECTORY}}", Encode(configuration.VirtualDirectory), System.StringComparison.Ordinal);
    }

    private static string Encode(string value)
    {
        return JsonEncodedText.Encode(value).ToString();
    }
}
