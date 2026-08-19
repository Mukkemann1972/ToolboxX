# 🧰 Empfohlene Begleit-Tools

## Warum dieses Dokument?

Toolbox X will ein "Schweizer Taschenmesser" sein – aber ein gutes Taschenmesser
erfindet nicht jede Klinge neu, wenn es schon eine ausgezeichnete, kostenlose gibt.
Für zwei Aufgaben verlinken wir bewusst fremde Tools statt sie nachzubauen: Rufus
(bootfähige USB-Sticks) und RustDesk (Fernwartung). Beide sind selbst Open Source,
beide kostenlos nutzbar, beide besser als alles, was wir in vernünftiger Zeit selbst
hinbekommen würden. Dieses Dokument hält fest, WARUM, damit die Entscheidung nicht in
einem Chat-Verlauf verloren geht.

In der App selbst stehen sie kurz auf der Entstehung-Seite ("Empfohlene
Begleit-Tools") – hier die ausführliche Fassung.

## RustDesk – Fernwartung

**Was es ist:** Ein Open-Source-Fernwartungstool (TeamViewer-/AnyDesk-Alternative),
in Rust geschrieben. Client UND Server sind quelloffen.

**Lizenz:** AGPL-3.0-or-later. Wichtig: sowohl der Client als auch der komplette
Server-Stack sind kostenlos nutzbar und selbst hostbar – kein Abo nötig, um die volle
Funktion zu bekommen. RustDesk verkauft zusätzlich "Server Pro" (ab ca. 9,90 $/Monat)
mit Enterprise-Extras wie zentraler Geräteverwaltung/Adressbuch – die kostenlose
Version bleibt davon unberührt, keine künstliche Beschneidung.

**Warum nicht selbst gebaut:** Ein eigenes Fernwartungstool wäre nicht nur doppelte
Arbeit, sondern ein komplett anderes Produkt-Genre (Netzwerkzugriff auf einen
FREMDEN Rechner, nicht lokale Diagnose/Reparatur) – und RustDesk macht das schon
technisch ausgereift und quelloffen.

**Praxisbezug:** Alex betreibt bereits einen eigenen RustDesk-Server auf seinem
Raspberry Pi und versorgt darüber Freunde/Familie mit Fernwartungszugriff – der
Selbsthosting-Aspekt ist hier kein theoretischer Vorteil, sondern schon im echten
Einsatz. Genau das macht RustDesk gegenüber TeamViewer/AnyDesk (beide Closed-Source,
serverseitig an den jeweiligen Hersteller gebunden) besonders wertvoll.

## Rufus – Bootfähige USB-Sticks erstellen

**Was es ist:** Ein Windows-Tool, um USB-Sticks aus ISO-Dateien bootfähig zu machen
(Windows-Installation, Rettungssysteme, Linux-Live-Systeme). Von Pete Batard
entwickelt, seit vielen Jahren der De-facto-Standard in der Reparaturbranche.

**Lizenz:** GPL-3.0-or-later, vollständig quelloffen (github.com/pbatard/rufus).

**Warum nicht selbst gebaut:** Bootfähige Medien korrekt zu erstellen (Partitions-
schema, Bootsektor, UEFI-vs-BIOS-Kompatibilität) ist eine Menge Detailarbeit mit
vielen Sonderfällen, die Rufus seit Jahren austestet und pflegt. Ein eigener Nachbau
wäre Zeitverschwendung ohne echten Mehrwert.

**Praxisbezug/Verknüpfung mit Toolbox X:** Rufus fragt beim Erstellen eines Sticks
nach dem Partitionsschema (GPT für UEFI, MBR für Legacy-BIOS) – viele Nutzer wissen
das nicht auswendig und wählen blind falsch. Genau dafür haben wir im
BIOS-Kompendium eine eigene Firmware-Typ-Erkennung gebaut (19.08.2026,
`FirmwareTypService`/`GetFirmwareType`), die live anzeigt, welche Einstellung zum
eigenen PC passt, und Rufus dabei explizit als Werkzeug nennt. Unser Installations-
Assistent (autounattend.xml-Generator) setzt außerdem voraus, dass der Nutzer sich
den eigentlichen Windows-Installationsstick selbst erstellt – typischerweise mit
Rufus oder Microsofts eigenem Media Creation Tool.

## Grundsatz für künftige Kandidaten

Bevor wir ein neues Modul bauen, das im Kern schon ein exzellentes, kostenloses,
offenes Tool abdeckt: erst prüfen, referenzieren, dann erst überlegen, ob ein
Eigenbau wirklich einen echten Mehrwert bringt (z.B. tiefere Toolbox-X-Integration,
Ampel-Erklärungen, die das fremde Tool nicht bietet) – nicht aus Prinzip alles
selbst schreiben.
