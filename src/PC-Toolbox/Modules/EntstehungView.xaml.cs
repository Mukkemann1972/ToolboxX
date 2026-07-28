using System.Windows.Controls;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class EntstehungView : UserControl
{
    public EntstehungView()
    {
        InitializeComponent();

        TitelTeil1.Text = Loc.T("Wie ", "How ");
#if PRO_BUILD
        TitelTeil2.Text = Loc.T(" Pro entsteht", " Pro comes to be");
#else
        TitelTeil2.Text = Loc.T(" entsteht", " comes to be");
#endif
        UntertitelText.Text = Loc.T("ECHTE ERFAHRUNG TRIFFT KI.", "REAL EXPERIENCE MEETS AI.");
        EinleitungText.Text = Loc.T(
            "Diese Toolbox schreibt sich nicht selbst - aber sie entsteht auch nicht allein von Alex. Ehrlich aufgeschlüsselt, wer/was woran gebaut hat, statt es zu verstecken:",
            "This toolbox doesn't write itself - but it also isn't built by Alex alone. Honestly broken down, who/what built what, instead of hiding it:");

        AlexRolle.Text = Loc.T("  ·  Vision, Entscheidungen, echte Tests", "  ·  Vision, decisions, real testing");
        AlexText.Text = Loc.T(
            "Setzt Richtung und Philosophie ('Nicht konsumieren - erschaffen'), entscheidet bei Zielkonflikten, und testet jedes neue Modul auf echter Hardware, bevor es als geprüft gilt. Viele Details (z.B. dass jeder BIOS-Eintrag ohne Vorwissen verständlich sein muss) kommen aus eigener Erfahrung mit Nutzern, die niemanden zum Fragen haben.",
            "Sets direction and philosophy ('Don't consume - create'), decides between conflicting goals, and tests every new module on real hardware before it counts as verified. Many details (e.g. that every BIOS entry has to be understandable without prior knowledge) come from firsthand experience with users who have no one to ask.");

        ClaudeRolle.Text = Loc.T("  ·  Code, Architektur, Sicherheitskonzepte", "  ·  Code, architecture, safety concepts");
        ClaudeText.Text = Loc.T(
            "Schreibt den Code jedes Moduls im Dialog mit Alex, von der ersten Idee bis zum fertigen Test. Baut dabei mit, was schiefgehen kann: das 3-Phasen-Sicherheitsprinzip (Sichern - Ausführen - Verifizieren), die Trennung in eine plattformneutrale Kern-Bibliothek für echte automatisierte Tests, und die Optik dieser Oberfläche - inklusive dieser Seite hier.",
            "Writes the code for every module in dialogue with Alex, from the first idea to the finished test. Builds in what can go wrong along the way: the 3-phase safety principle (back up - execute - verify), the separation into a platform-neutral core library for real automated tests, and the look of this interface - including this page here.");

        HermesRolle.Text = Loc.T("  ·  Recherche im Hintergrund", "  ·  Background research");
        HermesText.Text = Loc.T(
            "Ein eigenständiger KI-Agent, der auf Alex' Raspberry Pi läuft und aktiv recherchiert: wo bestehende Tools (Uninstall Tool, BestSync, Total Commander, ...) Nutzer im Stich lassen, was gefragt wird und noch fehlt. Ergebnisse fließen ins Ideen-Backlog und in neue Module - Hermes schreibt keinen Code für diese Oberfläche, sondern liefert die Grundlage für Entscheidungen.",
            "An independent AI agent running on Alex's Raspberry Pi that actively researches: where existing tools (Uninstall Tool, BestSync, Total Commander, ...) let users down, what's being asked for and still missing. Findings flow into the ideas backlog and new modules - Hermes doesn't write code for this interface, it provides the basis for decisions.");

        PerspektivenText.Text = Loc.T(
            "Nicht jedes Modul ist zu gleichen Teilen von allen dreien geprägt - aber jedes hat diese drei Perspektiven durchlaufen: technisch machbar (Claude), tatsächlich gebraucht (Hermes), auf echter Hardware bestätigt (Alex).",
            "Not every module is shaped equally by all three - but every one has gone through these three perspectives: technically feasible (Claude), actually needed (Hermes), confirmed on real hardware (Alex).");

#if PRO_BUILD
        FusszeileText.Text = Loc.T(
            "Erschaffen statt konsumieren · Für Werkstatt & IT-Profis · Kein Abo, keine Spyware",
            "Create instead of consume · For workshops & IT professionals · No subscription, no spyware");
#else
        FusszeileText.Text = Loc.T(
            "Erschaffen statt konsumieren · Kostenlos · Open Source · Kein Abo, keine Spyware",
            "Create instead of consume · Free · Open Source · No subscription, no spyware");
#endif
    }
}
