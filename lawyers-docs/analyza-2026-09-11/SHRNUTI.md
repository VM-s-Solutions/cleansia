# CleanSia: podklad pro jednání s právníky

**Srovnání 10 právních návrhů s procesy aplikace · 11. září 2026**  
Kontrolovaný stav: `master`, commit `e1d81ea29f10939bd0a278e777d8cc3138834964`.

## Co potřebuješ vědět před schůzkou

**Dokumenty pokrývají základ služby, ale v současné podobě nepopisují jeden sjednocený provozní model CleanSia.** Nestačí doplnit IČO, kontakty a datum. Některé podmínky přímo odporují aplikaci, část existujících funkcí nemá vlastní pravidla a některé dokumenty slibují postupy, jejichž plnění vyžaduje člověka mimo aplikaci.

Na schůzce bych nejdříve rozhodl těchto pět věcí:

1. **Kdo zákazníkovi poskytuje úklid a kdo komu fakturuje.** VOP a partnerská smlouva popisují zprostředkovatele, zatímco aplikace vytváří partnerské faktury vůči CleanSia a zákaznické doklady z údajů společnosti.
2. **Jaká finanční pravidla skutečně chceme.** Nesedí storno, procentní provize, vypořádání hotovosti ani peněžní vracení kreditů při ukončení účtu.
3. **Jaké znění zákazník přijímá a jak to prokážeme.** Web zobrazuje jiné krátké návrhy než dodané Wordy; evidence souhlasů neuchovává slíbené verze a úplnou historii.
4. **Jak popsat současné rozšířené služby.** Plus, automatické obnovování členství, opakované rezervace, kredity, loajalita a doporučování potřebují konkrétní podmínky.
5. **Kdo vlastní provozní povinnosti.** Reklamace, pojištění, školení, incidenty, výmazy údajů a ukončení spolupráce potřebují reálně obsluhované postupy.

**Pozitivní základ existuje:** aplikace má objednávky, platby, storno a refundace, reklamace, kontrolu partnerských dokladů, průběh úklidu, fotografie, výpočet odměn, cookie lištu a nástroje pro souhlasy, export a výmaz. Problémem je především nesoulad jejich konkrétních pravidel s návrhy dokumentů.

## 1. Co je v jednotlivých dokumentech

„Rozpor“ znamená doložené jiné chování kódu. „Částečné pokrytí“ znamená, že základ existuje, ale chybějí významné podmínky. „Mimo aplikaci“ znamená povinnost, kterou lze plnit ručně a jejíž skutečné plnění tento rozbor neověřoval.

| Dokument | Co řeší | Výsledek a hlavní práce pro právníky |
|---|---|---|
| [VOP](../SMLOUVY%20CleanSia/SMLOUVY/Cleansia_VOP.docx) | Strany smlouvy, účet, objednávku, cenu, platbu, změny, storno, odpovědnost, recenze a spory. | **Rozpory:** obchodní model, host bez účtu, storno, kredity. Doplnit Plus a další programy; sjednotit s publikovaným textem. |
| [Reklamační řád](../SMLOUVY%20CleanSia/SMLOUVY/cleansia_reklamacni_rad.docx) | Vady, škody, krádež, podání a lhůty, důkazy, nápravu, refundaci, pojištění a eskalaci. | **Částečné pokrytí + provoz:** systém případů funguje; konkrétní potvrzování, lhůty, nápravný úklid a pojistný postup vyžadují sjednocení a odpovědnou obsluhu. |
| [Rámcová smlouva](../SMLOUVY%20CleanSia/SMLOUVY/cleansia_ramcova_smlouva.docx) | Nezávislost OSVČ, výběr práce, vybavení, pojištění, provizi, hotovost, odpovědnost, DPA a konec spolupráce. | **Rozpory:** provize versus odměna, fakturační model. **Nedostatečné pokrytí:** firmy a týmy. Doplnit ceník, výplatní cyklus a podmínky aktivace partnera. |
| [BOZP](../SMLOUVY%20CleanSia/SMLOUVY/cleansia_bozp.docx) | Chemikálie, ochranné pomůcky, spotřebiče, ergonomii, první pomoc, úrazy, zvířata, výšky, odpad a opakování poučení. | **Převážně mimo aplikaci:** ověřit odborný obsah, školitele, evidenci poučení, obnovy a skutečný kontakt dispečinku. |
| [Kodex chování](../SMLOUVY%20CleanSia/SMLOUVY/cleansia_kodex_chovani.docx) | Vystupování, oděv a vůni, příchod a předání, fotografie, komunikaci, nalezené věci, sociální sítě a sankce. | **Rozpory + provoz:** hotovost, fotografie a některé zákazy nejsou sladěné s rámcovou smlouvou. Upozornění, pozastavení a distribuce vybavení nejsou totéž co obecná správa účtu. |
| [Privacy Policy](../SMLOUVY%20CleanSia/SMLOUVY/cleansia_privacy_policy.docx) | Správce, účely a údaje, právní tituly, příjemce, retenci, bezpečnost, práva a věk. | **Částečné pokrytí / rozpory:** doplnit skutečné dodavatele a data, přepsat retenční tabulku a ověřování věku podle zamýšleného procesu. |
| [Cookie Policy](../SMLOUVY%20CleanSia/SMLOUVY/cleansia_cookie_policy.docx) | Veřejný cookie text a interní implementační příručku. | **Rozpory:** názvy úložišť, evidence a platnost souhlasu. Vzorový seznam analytiky není doložený skutečnou implementací. Interní část B oddělit od veřejného dokumentu. |
| [GDPR souhlasy](../SMLOUVY%20CleanSia/SMLOUVY/cleansia_gdpr_souhlasy.docx) | Checkboxy, registraci, objednávku, GPS, marketing, odvolání a historii souhlasů. | **Zásadní rozpory:** přepisovaná evidence bez verze, směšování cookies s e-maily, hostovská objednávka, význam povinného souhlasu a GPS. |
| [DPA](../SMLOUVY%20CleanSia/SMLOUVY/cleansia_dpa.docx) | Zpracování zákaznických dat partnerem, přístup, mlčenlivost, retenci, incidenty, subzpracovatele a výmaz. | **Částečné pokrytí:** omezení podle přidělení existuje; historický partner má dále přístup bez sjednaného časového omezení. Podpisy a práce s kopiemi mimo aplikaci potřebují evidenci. |
| [Společenská smlouva](../SMLOUVY%20CleanSia/SMLOUVY/cleansia_spolecenska_smlouva.docx) | Založení a řízení s.r.o., podíly, vklady, jednatele, hlasování, zisk, převody a zánik. | **Samostatné korporátní téma:** pracovní návrh pro notáře. Doplnit skutečné osoby a parametry, sladit hlasovací ustanovení, vyřešit práva k softwaru. Nejde o funkci aplikace. |

Napříč balíčkem zůstávají nevyplněné identifikační údaje, kontakty, účinnost a některé zásadní obchodní parametry. Číslo verze „1.1“ samo nepotvrzuje právní schválení ani shodu s tím, co vidí uživatel.

## 2. Největší rozpory, které je potřeba rozhodnout

### A. Role stran, vznik smlouvy a fakturace

VOP 1.3 a rámcová smlouva 2.2–2.3 popisují přímý vztah zákazník–OSVČ. Už mezi návrhy je rozdíl v okamžiku vzniku smlouvy: VOP jej spojují s potvrzením/platbou, rámcová smlouva s přijetím zakázky partnerem. V aplikaci může být zakázka po platbě „Confirmed“, přestože nemá přiděleného pracovníka. Vícečlenný úklid může mít několik samostatných partnerských účtů.

Partnerská faktura uvádí partnera jako dodavatele a CleanSia jako odběratele; zákaznický doklad používá údaje společnosti. **Rozhodnout jeden smluvní a účetní model**, identitu dodavatele, oprávnění vystavovat doklady a odpovědnost za celý tým. Samotný kód neurčuje právní kvalifikaci vztahu.

### B. Storno je v dokumentu výrazně dražší

| Situace | VOP čl. V | Aktuální standardní pravidlo aplikace |
|---|---|---|
| Více než 24 hodin před úklidem | Bez poplatku | Bez poplatku; zdarma je i přesně 24 hodin. |
| 4 až méně než 24 hodin | 50 % | 25 % |
| 2 až méně než 4 hodiny | 50 % | 50 % |
| Méně než 2 hodiny | 100 % | 50 % |
| Nikdo zakázku nepřijal | Samostatná výjimka není popsána | Zdarma |
| Prvních 15 minut od objednání | Samostatná výjimka není popsána | Zdarma |

Tabulka ukazuje standard po přijetí zakázky, s posledními dvěma přednostními výjimkami. Plus může bezplatné storno dále zvýhodnit. Zahájenou nebo dokončenou zakázku zákaznickým stornem zrušit nelze. VOP navíc uvádějí 100 % při znemožnění přístupu; zvláštní automatický tarif pro tuto situaci v prověřené cestě není doložen.

**Rozhodnout sazby a výjimky; teprve pak sjednotit kód, VOP a obrazovky.** Zmínku o 60 minutách zdarma pro první objednávku ze starší dokumentace nepřebírat: aktuální volající tuto výhodu nepřiznává.

### C. Odměna partnera není procentní provize z každé objednávky

Rámcová smlouva čl. 7 předpokládá procentní provizi. Aplikace počítá odměnu ze sazeb za služby/balíčky, místnosti, koupelny a vzdálenost s limity a individuálními nastaveními. Nejde automaticky o zákaznickou cenu minus například 20 %. **Schválit skutečný ceník, okamžik závaznosti odměny a vliv zákaznických slev.**

Hotovost aplikace podporuje; automatické smluvní započtení platformní provize však není doloženo. Kodex přitom zakazuje platby mimo aplikaci. Vyjasnit, zda zákaz míří na zatajené zakázky, nebo i na povolené hotovostní inkaso. Fakturační evidence sama neprovádí bankovní výplatu.

Automatika vytváří měsíční výplatní období navázaná na den založení prvního období; PDF faktura má splatnost 14 dní. Rámcová smlouva 7.4 zatím nechává cyklus a lhůtu výplaty nevyplněné. Potvrdit, zda to odpovídá zamýšlenému účetnímu provozu.

### D. Host, publikované podmínky a důkaz přijetí

VOP vyžadují registraci, ale API přijímá objednávky hostů. Hostovský checkbox na webu ukládá záznam místně a odeslání čeká na přihlášení; samotná objednávka nenese verzi přijatých podmínek. Evidence souhlasů přepisuje aktuální stav, místo aby uchovávala slíbenou neměnnou historii a verzi textu.

Webové `/terms` a `/privacy` navíc používají krátké překladové návrhy s upozorněním na právní nezkontrolování a prázdným datem. **Schválit jeden obsah pro web i mobilní aplikace, jeho překlady a způsob doložení přijetí pro účet i hosta.** Rozlišit přijetí smlouvy, seznámení s informací a odvolatelný souhlas.

### E. Kredity a konec účtu

VOP 2.4 slibují peněžní vrácení nevyužitého kreditu. Aplikace výmaz účtu s kladným kreditem blokuje. Kredit expiruje po 12 měsících od posledního pohybu, používá se automaticky na kartovou objednávku ve stejné měně a pokryje nejvýše 70 % ceny. Samostatný peněžní výběr zůstatku nebyl doložen.

**Oddělit bonus od peněžního nároku zákazníka**, stanovit expiraci, čerpání, vypořádání a vztah k výmazu účtu. Zrušení neobsazené zakázky umí vrátit platbu a přidat registrovanému zákazníkovi 250 kreditních jednotek v základní měně; host tento kredit nedostane. To také potřebuje jasná pravidla.

### F. Fotografie a přístup k údajům po zakázce

Standardní partnerské dokončení úklidu vyžaduje alespoň jednu fotografii „After“; administrátorská změna stavu má samostatnou cestu. Rámcová smlouva a Kodex pracují s fotografováním na základě souhlasu a omezenými výjimkami; nahrání fotografie samo souhlas neeviduje. **Rozhodnout účel, právní titul, informování a cestu pro zákazníka, který fotografii odmítne.**

DPA stanoví partnerovi krátké lhůty, například 24 hodin pro kontakt/adresu a 30 dní pro provozní údaje/fotografie. Historicky přidělený partner však může přes API nadále načíst detail i nové odkazy na fotografie. Delší uchování platformou není automaticky totéž co porušení partnerovy retenční povinnosti; je nutné zvlášť vyřešit **platformové uchování, přístup partnera a jeho vlastní kopie**.

### G. Cookies, marketing, GPS a retence

Volba marketingových cookies se na serveru mapuje na souhlas s marketingovými e-maily. To směšuje dvě rozdílná rozhodnutí; nález sám nedokazuje rozesílání nevyžádaných zpráv. Lišta také neukládá slíbené datum, verzi a dobu platnosti souhlasu.

Text GPS slibuje, že se poloha nesdílí s třetími stranami, ale Android při převodu souřadnic na adresu volá Mapbox. Privacy uvádí jiné retenční lhůty než výchozí automatické mazání. Návrhy pracují s hranicí 18 i 16 let, zákaznická registrace věkové ověření nemá. **Připravit jednu schválenou tabulku účel–údaj–příjemce–lhůta–právní titul** a podle ní upravit texty i implementaci.

### H. Reklamace, pojištění a školení potřebují doložený provoz

Reklamační případ, přílohy, komunikace, rozhodnutí a kartová refundace existují. Kód ale nedokládá celé slibované potvrzení do 30 minut, třídenní prošetření, nápravný úklid či aktivaci pojištění. Krátké lhůty a dvě povinné fotografie z řádu nejsou shodně vynuceny; pozdní podání systém přijme.

Schválení partnera ověřuje konfigurované typy dokladů. Uložený seed pro CZ/SK vyžaduje občanský průkaz; pojištění, podepsaná smlouva a školení z něj automaticky povinné nejsou. Živé nastavení může být jiné. **Doložit skutečný partnerský spis, checklist, obnovy pojištění a školení, reklamační frontu a obsluhované kontakty.** Chybějící automatizace není důkaz, že se nic nedělá ručně.

## 3. Procesy, které mají v dokumentech nedostatečná pravidla

| Existující proces | Co je potřeba doplnit |
|---|---|
| **Cleansia Plus** | Cena a perioda, automatické obnovení, přechod trialu na placení, doúčtování při změně plánu, zrušení na konci období a nárok na vrácení platby. |
| **Opakované rezervace** | Rozvrh versus jednotlivá objednávka, samostatné potvrzení a platba výskytu, aktuální cena, pauza a konec série. Označení „Monthly“ dnes v generování znamená 30 dní. |
| **Loajalita, doporučování a promokódy** | Vznik a odebrání bodů, limity, kombinování slev, změny kampaní, zneužití a odlišení bodů od kreditu a peněz. |
| **Cena, express, termín a tým** | Význam odhadu ceny/délky, příplatky a rozsah balíčků, čtvrthodinový výběr v rámci 60minutového okna, počet pracovníků a nezaručená preference oblíbeného partnera. |
| **Neobsazená zakázka a nedostavení** | Automatické zrušení neobsazené zakázky po toleranci není detekce nepříchodu již přiděleného pracovníka. Popsat refundaci, kredit a lidskou eskalaci. |
| **Firmy, týmy a náhradníci** | Právnická osoba versus OSVČ, vazba členů týmu na smluvního poskytovatele, odpovědnost a přístup k datům. Žádost o zástup/opuštění je doložena na backendu, nikoli jako úplná cesta v uživatelských obrazovkách. |
| **Omezení a konec spolupráce** | Důvody omezení zakázek, přezkum člověkem, upozornění, výpověď, pozastavení, dokončení rozpracované práce a konečné vyúčtování. Technická blokace účtu nenahrazuje právní úkon. |
| **Údaje zákazníků i partnerů** | Fotky domácností, důkazy, vstupní instrukce, identity a doklady, bankovní údaje, zařízení, push tokeny, diagnostika a referral vazby; konkrétní příjemci včetně mapových služeb. |
| **Práva subjektů údajů** | Samoobslužný export obsahuje jen část evidovaných dat; určit doplnění na žádost, výjimky z výmazu, neúspěšné smazání souborů a vyřízení partnerovy žádosti člověkem. |

Opačným směrem návrhy slibují některé funkce, pro které nebyla nalezena odpovídající kompletní cesta: obecný samoobslužný přesun jednorázové objednávky, platformní spropitné, zákaznický bankovní převod a peněžní výběr kreditu. Mohou být řešeny podporou či účetní; dokument má tuto cestu popsat konkrétně.

## 4. Samostatné otázky pro právní posouzení

**Spotřebitelská pravidla:** prověřit odkaz VOP čl. V na § 1837 písm. j) jako důvod vyloučení 14denního odstoupení u úklidu s termínem. Z oficiálního přehledu ČOI nelze převzít obecnou výjimku pro každou službu objednanou na konkrétní den; uvádí vymezené kategorie služeb. Právníci mají určit správný režim úklidu a případnou výslovnou žádost o zahájení plnění před uplynutím lhůty. [ČOI – informace k odstoupení u služeb](https://coi.gov.cz/faq/5-odstoupeni-od-smlouvy-do-14-dnu-u-sluzeb-4/).

Dále nechat posoudit 24/48hodinové reklamační lhůty, povinné fotografie, vztah ke skrytým vadám, prodloužení 30denní lhůty pouhým oznámením, omezení odpovědnosti a smluvní sankce. To jsou otázky právní platnosti, nikoli závěr tohoto technického srovnání.

**Zastaralý odkaz:** VOP 12.2 a reklamační řád 5.4 stále odkazují na ODR. Platforma skončila 20. 7. 2025; informace o příslušném ADR tím obecně nezaniká. Text opravit. [MPO – ukončení ODR a trvající informační povinnost ADR](https://mpo.gov.cz/cz/ochrana-spotrebitele/mimosoudni-reseni-spotrebitelskych-sporu-adr/skoncila-informacni-povinnost-obchodniku-o-platforme-odr--288708/).

**Společnost a zakladatelé:** společenská smlouva je pracovní návrh s nevyplněnými společníky, podíly, vklady a rozhodnými limity. Čl. 5.4 uvádí pro změnu smlouvy dvoutřetinovou většinu; 10.6 souhlas všech, pokud zákon nestanoví jinak. Jejich vztah má sjednotit notář. Předmět podnikání zahrnuje zprostředkování i poskytování úklidu; samotná šíře předmětu není rozpor, ale neurčuje, jakou roli má konkrétní zákaznická smlouva.

V těchto deseti souborech není doloženo vypořádání autorských práv k softwaru od zakladatelů a dodavatelů, vlastnictví domén či účtů aplikací ani podrobná dohoda pro patovou situaci mezi zakladateli. Mohou existovat jinde. **Prověřit, co už je uzavřeno a co doplnit samostatně.** Také instrukce pro založení společnosti, poplatky a lhůty v návrhu nechat ověřit notářem.

## 5. Co si odnést z dnešního jednání

| Pořadí | Konkrétní výstup | Kdo rozhoduje / připraví |
|---|---|---|
| 1 | Jedna stránka popisující smluvní strany, okamžik smlouvy, platby, doklady a odpovědnost týmu. | Vedení + právníci + účetní |
| 2 | Jedna schválená tabulka cen, storna, odměn, hotovosti, kreditů a výplat. | Vedení + účetní; právní kontrola |
| 3 | Rozhodnutí, které služby a benefity budou při spuštění dostupné, a příslušná pravidla. | Vedení + právníci |
| 4 | Schválený způsob přijímání textů, marketingových voleb, fotografií a uchování důkazů. | Právníci + odpovědná osoba za produkt |
| 5 | Retenční a dodavatelská tabulka; checklist partnera, reklamací, incidentů a žádostí o údaje. | Právníci + provoz + technický tým |
| 6 | Seznam finálních veřejných dokumentů, podpisových příloh a interních postupů s vlastníkem a datem. | Právníci + vedení |

Po rozhodnutí rozdíly rozdělit na **úpravu textu**, **změnu aplikace** a **provozní postup**. Nesoulad není automatický pokyn změnit aplikaci podle dnešního Wordu; některé návrhy popisují jiný obchodní záměr a samy si odporují.

## Jak byl rozbor proveden a kde jsou důkazy

Přečteno všech deset DOCX včetně tabulek a textových částí; porovnáno s aktuálním backendem a relevantními cestami webu, Androidu a iOS. Dokumentace v `docs/` sloužila k orientaci, rozhodující byl prověřený kód. Například starší komentáře popírají automatický kredit u neobsazené zakázky, ale aktuální implementace již existuje.

Jde o statické srovnání procesů a návrhů, nikoli ověření jejich právní platnosti nebo provozu nasazeného prostředí. Nebyly kontrolovány živé databáze, skutečné pojištění a podpisy, bankovní platby, cloudové smlouvy, školení ani obsluha podpory. Konfigurovatelné výchozí hodnoty nemusí odpovídat nasazení. Původní právní dokumenty a zdrojový kód zůstaly beze změny.

Podrobné nálezy s články, odstavci a cestami do kódu jsou v [podrobném rozboru](PODROBNY-ROZBOR.md). Část ke společnosti je doložena [extrakcí společenské smlouvy](podklady/cleansia_spolecenska_smlouva.txt), zejména P005, P017, P031–P065, P079 a P127. Označení P je pomocné číslo odstavce extrakce, nikoli číslo článku originálu. [Soupis a kontrolní otisky podkladů](podklady/INVENTAR.md) vážou rozbor na konkrétní soubory.
