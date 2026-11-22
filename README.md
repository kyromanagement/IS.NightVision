# NightVisionManager 1.0.0

Plugin server C# di **Init Studio** per SCP: Secret Laboratory. Applica il potenziamento visivo nativo `NightVision` al singolo giocatore. I giocatori possono usare il client standard e le luci della struttura restano intatte.

| Componente | Versione di riferimento |
| --- | --- |
| SCP: Secret Laboratory Dedicated Server | 14.2.9 |
| EXILED | 9.15.3 stabile |
| LabAPI | 1.1.7, inclusa nel server dedicato |
| SDK di build | .NET 8 o successivo. Target `net48`. |

## Compilazione e installazione

1. Installa il server dedicato 14.2.9 e trova la sua directory `SCPSL_Data/Managed`. Installa [EXILED 9.15.3](https://github.com/ExMod-Team/EXILED/releases/tag/v9.15.3) con i suoi moduli, incluso `Exiled.CustomRoles`. La LabAPI del server è sufficiente.
2. Compila con l’SDK .NET. Su Windows usa lo stesso comando con il percorso locale tra virgolette.

   ```sh
   dotnet build NightVisionManager/NightVisionManager.csproj -c Release \
     -p:SlManagedDir="/percorso/server/SCPSL_Data/Managed"
   ```

3. Copia solo `NightVisionManager/bin/Release/net48/NightVisionManager.dll` in `EXILED/Plugins` e avvia il server. Il plugin genera il YAML commentato in `EXILED/Configs/<porta>/night_vision_manager.yml`. Il bootstrap EXILED ha anche il normale flag di caricamento `is_enabled`.
4. Modifica il [YAML operativo](night_vision_manager.yml) del server e usa `nightvision reload` in Remote Admin. Il reload valida il file e la disponibilità dell’ID del keybind prima di applicare le modifiche. Se il file o l’ID sono invalidi, rimane attiva l’ultima configurazione valida. Se il primo caricamento fallisce, correggi il file e ripeti il comando. In caso di conflitto all’avvio il plugin lascia libero il keybind e conserva la modalità console configurata. Cambia l’ID o disattiva il keybind nel YAML, poi ricarica.

## Accesso al visore

L’accesso parte disabilitato per ogni ruolo. Il file iniziale abilita esplicitamente solo i quattro ruoli NTF. Le regole si applicano in quest’ordine, campo per campo:

1. **Team nativo** come `FoundationForces` o `ChaosInsurgency`.
2. **Gruppo di ruoli** `Ntf` (Private, Sergeant, Captain e Specialist), `FacilityGuards` (`FacilityGuard`) o `Tutorial`. `Tutorial` resta il ruolo nativo del gioco.
3. **Ruolo vanilla** sotto `roles`, per esempio `NtfSpecialist: { enabled: false }`.
4. **Ruolo EXILED registrato** per ID sotto `custom_roles`. Se più ruoli si sovrappongono, l’ID numerico più basso prevale sui campi che specifica. Gli altri gestori di ruoli richiedono un’integrazione propria.

Un’assegnazione Remote Admin può concedere o revocare l’accesso e selezionare un profilo per la vita corrente. Le condizioni di utilizzo e il flag generale si applicano anche agli override. Il plugin si limita ai giocatori verificati, vivi, in un ruolo in prima persona. La morte, il cambio di ruolo, la disconnessione e la fine del round chiudono la sessione. Un ruolo personalizzato rimosso senza cambio del ruolo vanilla viene rivalutato al successivo aggiornamento periodico.

| Condizione | Uso |
| --- | --- |
| `Always` | Può attivarsi in qualunque posizione. |
| `BlackoutOnly` | Richiede un blackout di stanza o zona di SCP-079 osservato durante il round e luci spente nella posizione del giocatore. Un blackout già in corso quando il plugin viene caricato per la prima volta non è attribuibile. Il reload del solo YAML conserva i blackout già osservati. |
| `DarkRoomsOnly` | Richiede luci locali spente secondo il controllo nativo, anche per cause diverse da SCP-079. |
| `SelectedZones` | Richiede una delle zone elencate per la regola. |
| `Disabled` | Blocca l’attivazione. |

L’uscita da una condizione valida spegne il visore. `auto_activate: true` tenta l’accensione una volta dopo il cambio di ruolo, se le condizioni sono già soddisfatte. Per riprodurre l’esempio con tutti gli NTF accesi alla generazione, abilita questo campo sotto `teams.Ntf`.

## Comandi e tasto

Usa **N** come tasto suggerito nei **Server-Specific Settings**, oppure digita **`.nvg`** nella console di gioco. Il giocatore può associare un altro tasto. Il server può cambiare il suggerimento e disabilitare in modo indipendente il tasto o il comando. Lo stesso cooldown si applica a entrambe le entrate. L’ID SSS evita collisioni con altre impostazioni registrate e può essere cambiato dal gestore del server quando necessario.

In Remote Admin sostituisci `<id>` con l’ID numerico della sessione del giocatore. Assegna i nodi ai gruppi amministrativi tramite EXILED. I nomi seguenti sono i valori predefiniti del YAML.

| Comando | Permesso | Risultato |
| --- | --- | --- |
| `nightvision grant <id> [profilo]` | `nightvision.grant` | Assegna al giocatore per la vita corrente. |
| `nightvision remove <id>` | `nightvision.remove` | Revoca per la vita corrente e rimuove gli effetti gestiti. |
| `nightvision reset <id>` | `nightvision.grant` e `nightvision.remove` | Ripristina la regola e il profilo YAML per il giocatore. |
| `nightvision on <id> [secondi]` | `nightvision.toggle` | Accende se la regola lo consente. La durata è opzionale e usa il punto decimale. |
| `nightvision off <id>` | `nightvision.toggle` | Spegne il visore attivo gestito dal plugin. |
| `nightvision status <id>` | `nightvision.status` | Mostra stato, profilo, intensità gestita, regola e condizioni. |
| `nightvision profile <id> <profilo>` | `nightvision.profile` | Sceglie un profilo esistente per la vita corrente. |
| `nightvision reload` | `nightvision.reload` | Valida e ricarica il YAML e rivaluta i visori attivi. |

## Resa visiva e integrazioni

`intensity` è il guadagno nativo al buio o dove il controller locale è sconosciuto. `lit_intensity` è il guadagno con le luci accese ed eredita `intensity` se omesso. L’accensione parte dal guadagno appropriato alla posizione. Durante l’uso, l’adattamento interpola fra i valori interi al cambio di stato delle luci. La durata si configura in `adaptation.seconds`. A impostazione disabilitata vale sempre `intensity`. La luminosità effettiva dei pixel, le torce e il post-processing restano gestiti dal client.

Allo spegnimento volontario, `Blurred` e `Concussed` sono attivabili separatamente, con durata predefinita di 1,5 secondi. Un effetto nativo già attivo per un’altra fonte viene lasciato al suo gestore. Per ciascun tipo di effetto il gioco mantiene un unico stato. I casi in cui un altro plugin riscrive valori identici simultaneamente non sono distinguibili tramite l’API.

Le tonalità Green, White Phosphor, Red e Blue, i parametri di shader e l’abbagliamento adattivo richiedono controlli lato client. La V1 usa esclusivamente l’amplificazione nativa per il visore. Questa influisce anche sulle regole di osservazione di SCP-173 e SCP-096 al buio. L’accesso, lo stato delle luci e l’applicazione degli effetti sono in componenti distinti per eventuali estensioni delle API native.

Per verificare la resa, le transizioni e i casi limite, usa la [checklist di collaudo con client reali](TEST-IN-GIOCO.md).
