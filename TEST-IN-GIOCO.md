# NightVisionManager 1.0.0 · Checklist di collaudo

Stato iniziale: da eseguire. Usare SCP: SL 14.2.9, EXILED 9.15.3 e LabAPI 1.1.7. Collegare almeno due client standard con impostazioni video equivalenti. Assegnare a un account i nodi di amministrazione e usare anche un account senza nodi.

## Attivazione ed effetti

- [ ] Su due NTF nella stessa stanza, accendere il visore per uno solo con `N`. Controllare il guadagno sul client attivo, la visuale invariata sul secondo e lo stato intatto delle lampade.
- [ ] Tenere premuto il tasto. Verificare una commutazione sola. Rilasciare, ripremere e verificare lo spegnimento. Provare il primo input dopo connessione e dopo respawn.
- [ ] Provare `.nvg` dalla console. Alternarlo rapidamente con il tasto per confermare il cooldown condiviso. Verificare i due flag di disabilitazione indipendenti.
- [ ] Riassegnare il tasto dal client. Provare un suggerimento server diverso da `N`, il ritorno a `N` e un ID SSS già occupato, verificando che l’altra impostazione continui a funzionare.
- [ ] Usare i profili Low, Standard e High sullo stesso client e nella stessa stanza. Verificare intensità diverse via `nightvision status` e osservare la resa.
- [ ] Con `Always`, accendere e spegnere le luci mediante meccaniche del gioco. Controllare la transizione da `intensity` a `lit_intensity` e viceversa. Ripetere con cambio stanza, riaccensione a metà della transizione e adattamento disabilitato. Verificare anche una posizione senza controller locale.
- [ ] Spegnere manualmente. Verificare `Blurred` per circa 1,5 s e ripetere con durata 1 e 2 s, con `Concussed` da solo e con entrambi i flag disabilitati. Controllare che il passaggio alle luci accese produca solo la variazione di guadagno.
- [ ] Applicare da Remote Admin o da un altro plugin `NightVision`, `Blurred` o `Concussed` allo stesso giocatore. Verificare che NightVisionManager non rimuova uno stato esterno già presente o chiaramente subentrato mentre il visore era attivo.
- [ ] Accendere durante un blackout e provare l’interazione con SCP-173 e l’osservazione del volto di SCP-096. Registrare le differenze rispetto al client del giocatore senza visore.

## Regole, comandi e ciclo di gioco

- [ ] Verificare le impostazioni iniziali con ciascun ruolo NTF, con uno dei quattro ruoli Chaos, un Facility Guard e Tutorial. Impostare `roles.NtfSpecialist.enabled: false` e verificare che gli altri NTF mantengano l’accesso.
- [ ] Impostare una squadra come disabilitata e autorizzare uno dei suoi ruoli via YAML. Provare anche un override del solo profilo. Con `auto_activate: true` controllare l’accensione al nuovo ruolo quando le condizioni sono soddisfatte.
- [ ] In `BlackoutOnly`, provare un blackout di stanza e uno di zona di SCP-079. Provare una stanza adiacente, le aree a più livelli, la fine del blackout e uno spegnimento delle luci prodotto da un’altra causa. In `DarkRoomsOnly`, verificare anche quest’ultimo caso.
- [ ] Provare `SelectedZones` dentro e fuori zona, `Disabled` anche con assegnazione amministrativa e una stanza priva di controller in `DarkRoomsOnly`.
- [ ] Registrare un ruolo con `Exiled.CustomRoles`. Provare applicazione e rimozione senza cambio del ruolo vanilla e due ID configurati sullo stesso giocatore.
- [ ] Verificare `grant`, `remove`, `reset`, `status`, `profile`, `on <id> <secondi>` e `off`. Ripetere con un account senza i relativi nodi, un ID inesistente e un profilo inesistente. Verificare anche che `.nvg` senza diritto non possa assegnare accesso.
- [ ] Ricaricare un YAML valido con giocatori attivi e cambiare profilo, condizione e flag generale. Ripetere durante un blackout, poi con YAML malformato, chiave duplicata e profilo inesistente. Controllare che una ricarica fallita conservi lo stato precedente.
- [ ] Provare morte, cambio ruolo, disconnessione, riuso dell’ID, inizio e fine round, disabilitazione e riabilitazione del plugin. Verificare che gli effetti gestiti e il keybind rimangano coerenti dopo la pulizia.
- [ ] Provare una sessione con molti portatori del visore. Controllare stabilità del tick del server, log e traffico di sincronizzazione a intensità stabile e durante una transizione di gruppo.
