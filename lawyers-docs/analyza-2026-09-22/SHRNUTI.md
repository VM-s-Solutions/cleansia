# Cleansia: obchodní mapa platformy

<style>
body { background:#e9eeeb; color:#183c38; }
main { max-width:930px; padding:42px; border:0; }
main > nav, main > h1 { display:none; }
.sheet { position:relative; min-height:260mm; padding:0 0 15mm; break-after:page; }
.sheet:last-child { break-after:auto; }
.sheet + .sheet { margin-top:45px; }
.sheet h1 { font-size:42px; line-height:1.08; color:#103e3a; margin:12px 0 18px; }
.sheet h2 { font-size:29px; line-height:1.18; color:#103e3a; border:0; padding:0; margin:10px 0 14px; }
.sheet h3 { font-size:18px; line-height:1.25; margin:0 0 7px; color:#103e3a; }
.sheet p { margin:7px 0 12px; }
.eyebrow { text-transform:uppercase; letter-spacing:1.7px; color:#59736c; font-size:11px; font-weight:700; }
.lead { font-size:18px; line-height:1.45; max-width:680px; }
.muted { color:#59736c; }
.grid2 { display:grid; grid-template-columns:1fr 1fr; gap:13px; margin:17px 0; }
.grid3 { display:grid; grid-template-columns:1fr 1fr 1fr; gap:11px; margin:17px 0; }
.card { padding:17px; background:#f0f5f1; border:1px solid #dce7df; border-radius:10px; break-inside:avoid; }
.card p:last-child { margin-bottom:0; }
.cream { background:#f6f2e7; border-color:#e7dfcc; }
.ink { background:#103e3a; color:#fff; border:0; }
.ink h3, .ink strong { color:#fff; }
.callout { padding:15px 18px; background:#fff0e9; border-left:4px solid #d87859; border-radius:0 9px 9px 0; margin:17px 0; break-inside:avoid; }
.callout p { margin:0; }
.stat { font-size:33px; line-height:1.1; font-weight:700; color:#176858; margin-bottom:8px; }
.small { font-size:13px; line-height:1.45; }
.sheet table { font-size:14px; line-height:1.4; margin:17px 0; }
.sheet th, .sheet td { padding:10px 12px; }
.sheet th { background:#dfece5; color:#103e3a; }
.sheet tr:nth-child(even) td { background:#f4f7f3; }
.sheet .two-col th:first-child { width:32%; }
.sheet .fees th:first-child { width:66%; }
.sheet .fees td:last-child { font-weight:700; font-size:18px; color:#176858; }
.diagram { width:100%; height:auto; display:block; margin:19px 0; }
.diagram text { font-family:'Segoe UI',Arial,sans-serif; }
.rule { border-top:1px solid #ceddd5; margin:20px 0 15px; }
.folio { position:absolute; bottom:0; left:0; right:0; display:flex; justify-content:space-between; border-top:1px solid #d6e0da; padding-top:9px; font-size:11px; color:#59736c; }
.q { display:grid; grid-template-columns:32px 1fr; gap:10px; margin:0 0 15px; break-inside:avoid; }
.q .num { width:27px; height:27px; border-radius:50%; background:#dfece5; color:#176858; text-align:center; padding-top:3px; font-weight:700; }
.q p { margin:3px 0 0; }
.q h3 { font-size:17px; }
@media print {
 @page { size:A4; margin:13mm; }
 body { font-size:10.5pt; line-height:1.4; background:white; -webkit-print-color-adjust:exact; print-color-adjust:exact; }
 main { margin:0; padding:0; max-width:none; }
 .sheet { min-height:260mm; }
 .sheet + .sheet { margin-top:0; }
 .sheet h1 { font-size:32pt; }
 .sheet h2 { font-size:23pt; }
 .sheet h3 { font-size:13pt; }
 .lead { font-size:12.5pt; }
 .eyebrow, .folio { font-size:8pt; }
 .small { font-size:9pt; }
 .sheet table { font-size:10pt; }
 .sheet th, .sheet td { padding:8px 10px; }
 .q h3 { font-size:12pt; }
 .card { padding:14px; }
 .grid2 { gap:12px; }
 .grid3 { gap:10px; }
}
@media screen and (max-width:650px) {
 main { padding:23px; }
 .grid2, .grid3 { grid-template-columns:1fr; }
 .sheet { min-height:0; padding-bottom:65px; }
 .sheet h1 { font-size:34px; }
}
</style>

<section class="sheet">
<div class="eyebrow">Podklad pro společné jednání · 23. září 2026</div>
<h1>Cleansia<br>Obchodní mapa platformy</h1>
<p class="lead">Od objednání úklidu přes peníze a práci úklidníka až po uchování údajů. Popis současného chování aplikací a otázek, které potřebují obchodní rozhodnutí.</p>
<svg class="diagram" viewBox="0 0 700 235" role="img" aria-label="Zákazník a úklidník používají platformu, objednávku vede regionální provozní společnost a obsluhuje administrace.">
<defs><marker id="party-arrow" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto-start-reverse"><path d="M0 0 L8 4 L0 8 Z" fill="#7f9c90"/></marker></defs>
<rect x="0" y="15" width="190" height="89" rx="12" fill="#dfece5"/>
<text x="95" y="51" text-anchor="middle" font-size="22" fill="#103e3a" font-weight="700">Zákazník</text><text x="95" y="79" text-anchor="middle" font-size="16" fill="#436359">účet nebo host</text>
<rect x="245" y="15" width="210" height="89" rx="12" fill="#103e3a"/>
<text x="350" y="51" text-anchor="middle" font-size="23" fill="white" font-weight="700">Cleansia</text><text x="350" y="79" text-anchor="middle" font-size="16" fill="#dfece5">objednávka a její evidence</text>
<rect x="510" y="15" width="190" height="89" rx="12" fill="#f4e7d5"/>
<text x="605" y="51" text-anchor="middle" font-size="22" fill="#103e3a" font-weight="700">Úklidník</text><text x="605" y="79" text-anchor="middle" font-size="16" fill="#436359">partnerský účet</text>
<path d="M197 60 H235" stroke="#7f9c90" stroke-width="2" marker-end="url(#party-arrow)"/><path d="M465 60 H500" stroke="#7f9c90" stroke-width="2" marker-end="url(#party-arrow)"/>
<path d="M350 113 V143" stroke="#7f9c90" stroke-width="2" marker-end="url(#party-arrow)"/>
<rect x="110" y="154" width="480" height="71" rx="12" fill="#f0f5f1" stroke="#dce7df"/>
<text x="350" y="183" text-anchor="middle" font-size="20" fill="#103e3a" font-weight="700">Regionální provozní společnost</text><text x="350" y="207" text-anchor="middle" font-size="16" fill="#436359">administrace · platby · doklady · podpora</text>
</svg>
<div class="grid2">
<div class="card"><h3>Zákazník</h3><p>Vybírá služby, místo a termín, platí a sleduje objednávku. Účet přidává historii, kredity, věrnost a reklamace. Host používá osobní odkaz z e-mailu; nemá všechny možnosti účtu.</p></div>
<div class="card"><h3>Úklidník</h3><p>Po schválení profilu přebírá nabízená místa, přijímá smlouvu, zaznamenává průběh a fotografie. Vidí přidělenou práci a své vyúčtování.</p></div>
<div class="card cream"><h3>Společnost a holding</h3><p>Organizační model počítá s holdingem a regionálními společnostmi. Objednávku vede společnost trhu, kde se uklízí. Katalog a cenové podklady se spravují společně.</p></div>
<div class="card cream"><h3>Administrace</h3><p>Správce, manažer, podpora a účetní mají rozdílná oprávnění. Obsluhují zakázky, úklidníky, reklamace, vratky, vyúčtování a nastavení společnosti.</p></div>
</div>
<div class="callout"><p><strong>Kdo je dodavatelem služby?</strong> Zákaznický doklad vystavuje provozní společnost, ale smlouva o dílo spojuje zákazníka s úklidníkem. Jednotné vysvětlení dodavatele, zprostředkování a fakturace je otevřený bod jednání.</p></div>
<div class="folio"><span>Cleansia · obchodní shrnutí</span><span>1 / 8 · Strany</span></div>
</section>

<section class="sheet">
<div class="eyebrow">Zakázka od výběru po dokončení</div>
<h2>Průběh úklidu a placení jsou oddělené</h2>
<p class="lead">Průběh úklidu a stav peněz se sledují odděleně. Zaplacení kartou samo o sobě neznamená, že zakázku převzal úklidník.</p>
<svg class="diagram" viewBox="0 0 700 164" role="img" aria-label="Výběr, objednání, převzetí, provedení a dokončení. Odděleně se eviduje platba.">
<defs><marker id="life-arrow" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto"><path d="M0 0 L7 3.5 L0 7 Z" fill="#7f9c90"/></marker></defs>
<g fill="#dfece5"><rect width="126" height="78" rx="9"/><rect x="143" width="126" height="78" rx="9"/><rect x="286" width="126" height="78" rx="9"/><rect x="429" width="126" height="78" rx="9"/><rect x="572" width="126" height="78" rx="9"/></g>
<g fill="#103e3a" text-anchor="middle" font-size="17" font-weight="700"><text x="63" y="33">Výběr</text><text x="206" y="33">Objednání</text><text x="349" y="33">Převzetí</text><text x="492" y="33">Provedení</text><text x="635" y="33">Dokončení</text></g>
<g fill="#436359" text-anchor="middle" font-size="14"><text x="63" y="57">cena a termín</text><text x="206" y="57">vznik zakázky</text><text x="349" y="57">úklidník + smlouva</text><text x="492" y="57">cesta a úklid</text><text x="635" y="57">fotografie výsledku</text></g>
<path d="M129 39 H139" stroke="#7f9c90" stroke-width="2" marker-end="url(#life-arrow)"/><path d="M272 39 H282" stroke="#7f9c90" stroke-width="2" marker-end="url(#life-arrow)"/><path d="M415 39 H425" stroke="#7f9c90" stroke-width="2" marker-end="url(#life-arrow)"/><path d="M558 39 H568" stroke="#7f9c90" stroke-width="2" marker-end="url(#life-arrow)"/>
<rect y="105" width="698" height="50" rx="9" fill="#f6f2e7"/>
<text x="349" y="137" fill="#745637" text-anchor="middle" font-size="17">Peníze: čeká na platbu · zaplaceno · částečně nebo plně vráceno</text>
</svg>
<div class="grid3">
<div class="card"><div class="stat">2 hodiny</div><p>Nejkratší předstih přímé rezervace. Mezi dvěma a čtyřmi hodinami se uplatní expresní příplatek 20 %, pokud jej nekryje členská výhoda.</p></div>
<div class="card"><div class="stat">8 + 4</div><p>Nejvýše osm pokojů a čtyři koupelny. Cenu tvoří služby, balíčky a doplňky; plocha v metrech čtverečních není cenovým vstupem.</p></div>
<div class="card"><div class="stat">Posádka</div><p>Počet míst počítá platforma z odhadu práce. Volba oblíbeného úklidníka znamená přednostní nabídku, nikoli záruku jeho účasti.</p></div>
</div>
<table class="two-col"><thead><tr><th>Situace</th><th>Co aplikace udělá</th></tr></thead><tbody>
<tr><td>Úklidník převezme místo</td><td>Zaznamená přijetí smlouvy pro dané místo. Zahájení vyžaduje přijatou smlouvu a přiřazení k zakázce.</td></tr>
<tr><td>Zakázka zůstane bez posádky</td><td>Automatická kontrola ji může zrušit po uplynutí půlhodinové tolerance od začátku; řeší vratku a u registrovaného zákazníka omluvný kredit.</td></tr>
<tr><td>Je potřeba jiný termín nebo rozsah</td><td>Vytvořená objednávka nemá běžnou změnu termínu ani navýšení ceny na místě. Obchodní postup je potřeba sjednotit s podmínkami.</td></tr>
<tr><td>Úklid je dokončen</td><td>Vyžaduje fotografii výsledku, smlouvu a odpovídající platební stav. Samotné označení „zaplaceno“ však u opakované hotovosti nedokládá výběr peněz.</td></tr>
</tbody></table>
<div class="card cream"><h3>Opakované úklidy</h3><p>Člen Plus může vytvořit šablonu. Platforma připravuje konkrétní termíny dopředu; zákazník je potvrzuje a řeší jejich platbu. „Měsíčně“ dnes obvykle vede k rozestupu 35 dní, protože se třicetidenní posun dorovnává na zvolený den týdne. Význam měsíčního opakování potřebuje rozhodnutí.</p></div>
<div class="folio"><span>Cleansia · obchodní shrnutí</span><span>2 / 8 · Život objednávky</span></div>
</section>

<section class="sheet">
<div class="eyebrow">Úhrada zakázky a odměna jsou různé pohyby</div>
<h2>Jak se pohybují peníze</h2>
<div class="grid2">
<div class="card"><h3>Karta</h3><p>Zákazník platí přes platební službu. Web používá platební pokladnu, mobil platební formulář. Potvrzená platba otevře objednávku k obsazení.</p><p>Kredit může uhradit nejvýše <strong>70 %</strong> ceny; zbytek jde z karty. Celková cena prodeje se použitím kreditu nemění.</p></div>
<div class="card cream"><h3>Hotovost</h3><p>Zákazník platí úklidníkovi na místě. Běžné potvrzení výběru zaznamená člověka a čas, nikoli skutečně převzatou částku.</p><p>Převzaté peníze se automaticky nezapočítávají proti odměně úklidníka. V aplikaci není hotovostní saldo ani automatické dorovnání.</p></div>
</div>
<svg class="diagram" viewBox="0 0 700 196" role="img" aria-label="Kartou zákazník platí přes platební službu. Hotovost předává úklidníkovi. Odměna úklidníka má samostatné vyúčtování a bankovní převod mimo aplikaci.">
<defs><marker id="money-arrow" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto"><path d="M0 0 L8 4 L0 8 Z" fill="#7f9c90"/></marker></defs>
<g fill="#f0f5f1" stroke="#dce7df"><rect x="0" y="5" width="150" height="64" rx="9"/><rect x="222" y="5" width="205" height="64" rx="9"/><rect x="501" y="5" width="198" height="64" rx="9"/><rect x="0" y="118" width="150" height="64" rx="9"/><rect x="222" y="118" width="205" height="64" rx="9"/><rect x="501" y="118" width="198" height="64" rx="9"/></g>
<g fill="#103e3a" font-size="18" font-weight="700" text-anchor="middle"><text x="75" y="43">Zákazník</text><text x="324" y="33">Platební služba</text><text x="600" y="33">Evidence úhrady</text><text x="75" y="156">Zákazník</text><text x="324" y="146">Úklidník</text><text x="600" y="146">Ruční vyrovnání</text></g>
<g fill="#59736c" font-size="14" text-anchor="middle"><text x="324" y="55">platba kartou</text><text x="600" y="55">a zákaznický doklad</text><text x="324" y="168">přebírá hotovost</text><text x="600" y="168">se společností</text></g>
<path d="M159 37 H212" stroke="#7f9c90" stroke-width="2" marker-end="url(#money-arrow)"/><path d="M437 37 H491" stroke="#7f9c90" stroke-width="2" marker-end="url(#money-arrow)"/><path d="M159 150 H212" stroke="#7f9c90" stroke-width="2" marker-end="url(#money-arrow)"/><path d="M437 150 H491" stroke="#7f9c90" stroke-width="2" marker-end="url(#money-arrow)"/>
</svg>
<div class="callout"><p><strong>Výjimka u opakované hotovosti:</strong> už potvrzení termínu zákazníkem označí objednávku jako zaplacenou. Následné běžné potvrzení výběru ji odmítne. Úklid pak může skončit bez záznamu, kdo a kdy hotovost převzal.</p></div>
<div class="grid2">
<div class="card"><h3>Doklad zákazníkovi</h3><p>Vystavuje jej provozní společnost. Položky, doplňky, slevy a expresní příplatek dávají uvedený součet. Doklad používá jazyk objednávky a místní datum úklidu.</p><p class="small">Po zaznamenání hotovosti se uložená účtenka přepíše na zaplacenou pod stejným číslem. Druhý e-mail se neposílá; host nemá stažení dokladu v aplikaci.</p></div>
<div class="card"><h3>Odměna úklidníka</h3><p>Výpočet vychází ze sazeb za služby a balíčky, velikosti a případné evidované vzdálenosti. Není to automatické procento ze zákaznické ceny ani měření odpracovaných hodin.</p><p class="small">Každý člen posádky dostává úplný výpočet. Měsíční vyúčtování a schválení jsou v aplikaci; bankovní převod provádí člověk mimo ni.</p></div>
</div>
<div class="folio"><span>Cleansia · obchodní shrnutí</span><span>3 / 8 · Peněžní toky</span></div>
</section>

<section class="sheet">
<div class="eyebrow">Storno, vratka a reklamace</div>
<h2>Kolik stojí zrušení objednávky</h2>
<p class="lead">Běžné zákaznické storno není dostupné během úklidu, po dokončení ani u už zrušené zakázky. Pokud je storno dostupné, rozhoduje čas a obsazení.</p>
<table class="fees"><thead><tr><th>Pravidlo pro zákazníka bez členské výjimky</th><th>Storno poplatek</th></tr></thead><tbody>
<tr><td>Zakázka nemá přiřazeného úklidníka</td><td>0 %</td></tr>
<tr><td>Do 15 minut od vytvoření objednávky</td><td>0 %</td></tr>
<tr><td>Alespoň 24 hodin před začátkem</td><td>0 %</td></tr>
<tr><td>Od 4 hodin do méně než 24 hodin před začátkem</td><td>25 %</td></tr>
<tr><td>Méně než 4 hodiny před začátkem</td><td>50 %</td></tr>
</tbody></table>
<p class="small muted">První dvě výjimky mají přednost. Plus může mít kratší hranici bezplatného storna podle konkrétního plánu. Delší, šedesátiminutová úleva pro první objednávku se neuplatňuje.</p>
<div class="grid2">
<div class="card"><h3>Vrácení peněz</h3><p>U karty se vratka provádí přes platební službu. Při kombinaci karty a kreditu se rozděluje podle původních zdrojů úhrady a dostupného zůstatku k vrácení.</p><p>Částečná vratka pracuje i s doplňky. Vratka sama nepřepočítává odměnu úklidníka a nevytváří opravný daňový doklad.</p></div>
<div class="card cream"><h3>Reklamace</h3><p>Podává ji přihlášený zákazník; host tento samoobslužný krok nemá. Uvádí důvod, popis a může dodat důkazy. Podpora rozhoduje a odpovídá.</p><p>Okno 24 hodin označuje včasnost, nezakazuje pozdější podání. Pokud rozhodnutí zahrnuje vratku, spor se při jejím selhání neuzavře jako vyřešený.</p></div>
</div>
<div class="card ink"><h3>Kde účetní a provozní návaznost chybí</h3><p>Hotovostní storno poplatek se eviduje, ale automaticky se nevybere. U rozhodnutí reklamace se může zapsaná požadovaná vratka lišit od skutečně přesunutých peněz. Zpětné zpochybnění webové karetní platby bankou může zůstat bez založeného sporu.</p></div>
<div class="callout"><p><strong>Účtenka není celá daňová agenda.</strong> Fiskální podklady neobsahují všechny cenové složky, které účtenka zobrazuje. Česká fiskální registrace dokladů není dokončena. Rozsah potřebné evidence, opravných dokladů a postupů je předmětem rozhodnutí pro konkrétní trh.</p></div>
<div class="folio"><span>Cleansia · obchodní shrnutí</span><span>4 / 8 · Zrušení a náprava</span></div>
</section>

<section class="sheet">
<div class="eyebrow">Čtyři pojmy, čtyři odlišné významy</div>
<h2>Plus, věrnost, sleva a kredit</h2>
<div class="grid2">
<div class="card"><h3>Plus je placené členství</h3><p>Výhody určuje plán: například členská sleva, bezplatné expresní rezervace v rámci kvóty nebo výhodnější storno. Členství umožňuje opakované rezervace.</p><p>Při nezaplaceném předplatném se výhody zastaví a členství nelze zrušit v aplikaci. Web nenabízí aktivní zkušební období; mobilní texty je stále slibují, ačkoli výchozí nabídka zkoušku neposkytuje.</p></div>
<div class="card cream"><h3>Věrnost jsou body a stupeň</h3><p>Dokončené objednávky a kvalifikované doporučení přinášejí body. Stupeň určuje výhody podle nastavení.</p><p>Body lze odebrat při vratce nebo ruční úpravě administrátorem. Stupeň se pak může snížit; nejde o nezrušitelný celoživotní status. Pravidlo poklesu je otevřená obchodní volba.</p></div>
<div class="card cream"><h3>Sleva snižuje cenu</h3><p>Členská a věrnostní sleva se skládají, společně nejvýše do <strong>12 %</strong>. Výhodnější promokód tuto kombinaci nahradí; nepřidává se nad ni.</p><p>Uplatnění se řídí také podmínkami konkrétní nabídky, měnou a případným minimem objednávky.</p></div>
<div class="card"><h3>Kredit je prostředek úhrady</h3><p>Hradí část ceny při platbě kartou, nejvýše <strong>70 %</strong>. Vede se podle měny a nevyplácí se automaticky v penězích.</p><p>Zůstatek propadá po <strong>12 měsících od posledního pohybu</strong>. Kladný kredit blokuje výmaz účtu, dokud se nevyřeší.</p></div>
</div>
<svg class="diagram" viewBox="0 0 700 165" role="img" aria-label="Nejprve se určí cena po slevách, potom se rozdělí úhrada mezi kredit a kartu.">
<defs><marker id="discount-arrow" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto"><path d="M0 0 L8 4 L0 8 Z" fill="#7f9c90"/></marker></defs>
<rect x="0" y="22" width="193" height="98" rx="12" fill="#f6f2e7"/><rect x="252" y="22" width="193" height="98" rx="12" fill="#dfece5"/><rect x="504" y="22" width="193" height="98" rx="12" fill="#103e3a"/>
<g font-size="19" font-weight="700" text-anchor="middle"><text x="96" y="59" fill="#103e3a">Nabídka služeb</text><text x="348" y="59" fill="#103e3a">Cena po slevách</text><text x="600" y="59" fill="white">Úhrada ceny</text></g>
<g font-size="15" text-anchor="middle"><text x="96" y="86" fill="#59736c">balíčky a doplňky</text><text x="348" y="86" fill="#59736c">včetně případného expresu</text><text x="600" y="83" fill="#dfece5"><tspan x="600">kredit a karta</tspan><tspan x="600" y="105">nebo hotovost</tspan></text></g>
<path d="M203 71 H242" stroke="#7f9c90" stroke-width="2" marker-end="url(#discount-arrow)"/><path d="M455 71 H494" stroke="#7f9c90" stroke-width="2" marker-end="url(#discount-arrow)"/>
<text x="348" y="151" text-anchor="middle" font-size="16" fill="#59736c">Použití kreditu cenu prodeje znovu nesnižuje.</text>
</svg>
<div class="callout"><p><strong>Nabídka musí odpovídat plnitelným výhodám.</strong> Ceny plánů, slevy, storno okna, pojistné částky a odměny v ukázkových datech nejsou schváleným ceníkem pro spuštění. Neplatné sliby zkušebního období v mobilu a nepodporované výhody potřebují sjednotit s reálnou nabídkou.</p></div>
<div class="folio"><span>Cleansia · obchodní shrnutí</span><span>5 / 8 · Výhody a hodnota</span></div>
</section>

<section class="sheet">
<div class="eyebrow">Údaje a automatický provoz</div>
<h2>Co zůstává a co se uklízí samo</h2>
<p class="lead">Platforma má výchozí lhůty uchování údajů. Společnost je může v povoleném rozsahu nastavit; smazání probíhá při následném automatickém běhu.</p>
<table class="two-col"><thead><tr><th>Údaje</th><th>Výchozí zacházení</th></tr></thead><tbody>
<tr><td>Fotografie úklidu</td><td>Po sedmi dnech od dokončení jsou určeny ke smazání. Neuzavřený spor je drží. Týdenní běh znamená zpravidla 7-13 dní; na přesné hranici až 14 dní. Nedokončené zakázky do tohoto pravidla nespadají.</td></tr>
<tr><td>Osobní údaje dokončených objednávek</td><td>Anonymizace po dvou letech od termínu úklidu. Nejde o smazání celého obchodního záznamu ani všech souvisejících údajů.</td></tr>
<tr><td>Záznamy činnosti zákazníka, administrátora a úklidníka</td><td>Tři roky podle stáří záznamu. U administrátorského a úklidnického auditu nastavení umožňuje i kratší dobu; minimální hranice vyžaduje rozhodnutí.</td></tr>
<tr><td>Oznámení a neaktivní zařízení</td><td>Výchozí okno 90 dní. U oznámení existuje také početní limit.</td></tr>
<tr><td>Nahrazené doklady úklidníků</td><td>Odstraňování neaktivních dokladů po 365 dnech od jejich deaktivace.</td></tr>
<tr><td>Odvolané souhlasy a další evidence</td><td>Odvolané souhlasy se po třech letech mažou. U dokončené žádosti o výmaz se po třech letech odstraní jen údaj o vyřizující osobě. Text sporu má tříletou lhůtu od výmazu účtu; IP adresa a zařízení na smluvním záznamu od přijetí.</td></tr>
</tbody></table>
<div class="grid2">
<div class="card"><h3>Přístup k domácnosti</h3><p>Přiřazený úklidník vidí adresu, kontakty a instrukce. První vydání neprázdných instrukcí se zaznamená. Přístup ale nemá časový konec po dokončení zakázky.</p><p class="small">Hostův osobní odkaz končí 30 dní po termínu úklidu; není jednorázový. Samotné zrušení objednávky neodvolává odkazy ve všech cestách.</p></div>
<div class="card cream"><h3>Výmaz účtu má hranice</h3><p>Otevřené zakázky, kredit či nevyrovnané odměny mohou výmaz blokovat. Sdílená adresa nesmí při výmazu poškodit jiného zákazníka.</p><p class="small">Zůstávají mezery: patro a byt, některé adresní záznamy a vadný pokus o smazání obrazových souborů při výmazu účtu. Nelze slibovat úplné odstranění všech stop.</p></div>
</div>
<div class="card ink"><h3>Automaticky / s člověkem</h3><p><strong>Automaticky:</strong> připomenutí úklidu, příprava opakovaných zakázek, rušení neobsazených rezervací, opakované doručování a zpracování dokladů, měsíční výplatní období, expirace kreditů a retenční úklid.</p><p><strong>S člověkem:</strong> schválení úklidníka, rozhodnutí reklamace, výjimky, schválení vyúčtování, skutečný bankovní převod a hotovostní vyrovnání.</p></div>
<div class="folio"><span>Cleansia · obchodní shrnutí</span><span>6 / 8 · Údaje a automatika</span></div>
</section>

<section class="sheet">
<div class="eyebrow">Jednání · obchodní a finanční rozhodnutí</div>
<h2>Co si potřebujeme společně potvrdit</h2>
<p class="lead">Ceník, smlouvy, peněžní toky a obrazovky musí vyprávět stejný příběh. Tyto otázky zůstávají otevřené.</p>
<div class="q"><div class="num">1</div><div><h3>Kdo prodává úklid a kdo komu fakturuje?</h3><p>Je provozní společnost dodavatelem, zprostředkovatelem, nebo jedná na základě příkazu? Jak s tím sladit smlouvu zákazníka s úklidníkem, zákaznický doklad, samofakturaci a případnou provizi?</p></div></div>
<div class="q"><div class="num">2</div><div><h3>Jaký ceník a odměňování skutečně chceme?</h3><p>Zůstanou služby a balíčky podle pokojů, nebo se přejde na plochu, pracnost a hodinovou sazbu? Kdo cenu a odměnu určuje? Má vícečlenná posádka násobit odměnu, rozdělit jednu částku, nebo zvýšit také cenu zákazníka?</p></div></div>
<div class="q"><div class="num">3</div><div><h3>Kdo drží a vyrovnává hotovost?</h3><p>Komu vzniká pohledávka, jak se eviduje skutečně převzatá částka a jak se započte proti odměně? Kdy se u opakované hotovosti smí objevit „zaplaceno“? Má hotovost zůstat dostupná bez limitu?</p></div></div>
<div class="q"><div class="num">4</div><div><h3>Má uložená karta sloužit jako záruka?</h3><p>Za jakých podmínek by se smělo inkasovat storno, neotevření bytu nebo nezaplacenou hotovost? Jak se to zákazníkovi vysvětlí? Jak se bude zacházet se spropitným?</p></div></div>
<div class="q"><div class="num">5</div><div><h3>Jak sjednotit DPH, doklady a vratky?</h3><p>Jaké plátcovství a ceny schválit pro provozní společnost? Mají sazby pro zákaznický prodej a vyúčtování úklidníků v témže trhu vycházet ze stejného pravidla? Kdo zajistí opravné doklady a požadovanou fiskální evidenci? Opravit chybějící webové bankovní spory a nesoulad požadované versus skutečné vratky hned?</p></div></div>
<div class="q"><div class="num">6</div><div><h3>Co zákazník dostane po zaplacení hotovosti?</h3><p>Má přijít druhý e-mail a má host dostat možnost stažení? Má doklad ponechat původní datum vystavení, nebo datum výběru? Jaký jazyk má mít firemní slogan a jak sjednotit zaokrouhlení nabídky s účtováním?</p></div></div>
<div class="q"><div class="num">7</div><div><h3>Jaké podmínky služby a výhod chceme slíbit?</h3><p>Patnáct minut bezplatného storna pro každého, nebo šedesát pro nového zákazníka, a jak počítat hosty? Kdy vzniká omluvný kredit, smí propadnout a vyplácí se při odchodu? Smí věrnostní stupeň klesnout? Jak odstranit neplatné sliby zkoušky Plus a umožnit zrušení členství při prodlení?</p></div></div>
<div class="q"><div class="num">8</div><div><h3>Co znamená termín, rozsah a objednávka v jiném trhu?</h3><p>Má měsíční úklid držet datum, pořadí dne v měsíci, nebo pevný interval? Platí starty 8:00-19:45 po čtvrthodinách pro všechny cesty objednání? Jak schválit práci navíc na místě, kdo přinese prostředky a jak se vyřeší objednávka mimo trh zákazníkova účtu?</p></div></div>
<div class="folio"><span>Cleansia · obchodní shrnutí</span><span>7 / 8 · Otázky k penězům a službě</span></div>
</section>

<section class="sheet">
<div class="eyebrow">Jednání · smlouvy, důvěra a spuštění</div>
<h2>Co musí mít jasného vlastníka</h2>
<div class="q"><div class="num">9</div><div><h3>Jaké znění se opravdu přijímá?</h3><p>Kdy se nahradí návrhy schválenými podmínkami, reklamačním řádem a dokumenty úklidníka? Kdo schválí jejich zveřejnění a opětovné přijetí? Jak smlouva ukáže strany, cenu každého místa v posádce, okamžik vzniku a důkaz souhlasu?</p></div></div>
<div class="q"><div class="num">10</div><div><h3>Jaká pravidla platí pro úklidníka a hosta?</h3><p>Je administrátorské přidělení závazné, nebo jde o nabídku, a jaké podmínky musí přidělený splnit? Jak sladit pracovní limity a povinné fotografie s nezávislou spoluprací? Zůstane host bez účtu a jak získá reklamaci, doklad a smluvní informace?</p></div></div>
<div class="q"><div class="num">11</div><div><h3>Co chrání domácnost a důkazy?</h3><p>Mají být fotografie povinné, včetně stavu před úklidem, a kdo je smí mazat? Stačí sedmidenní lhůta s týdenním mazáním a výjimkou již otevřeného sporu pro pozdější reklamace a bankovní spory? Co s nedokončenými zakázkami? Kdy ukončit přístup k adrese a instrukcím, jak doložit zpracovatelskou smlouvu a výmaz kopií ze zařízení?</p></div></div>
<div class="q"><div class="num">12</div><div><h3>Jakou ochranu údajů lze poctivě slíbit?</h3><p>Mají být tři roky minimem auditních záznamů? Co smí dlouhodobě zůstat na smluvním záznamu a jak odstranit známé zbytky při výmazu? Jaké cookies, marketing, polohové údaje a věkovou hranici skutečně používáme? Jak sladit mlčenlivost, možné souběhy pokut a konkurenční závazky v návrzích?</p></div></div>
<div class="q"><div class="num">13</div><div><h3>Kdo nese ruční závazky a komunikaci?</h3><p>Kdo řeší nedostavení úklidníka, neotevření bytu a škodu, s jakou náhradou a v jaké lhůtě? Kdo hlídá reklamaci a vratku? Co oznámíme, komu a jakým kanálem? Čí pojištění kryje škodu, kdo ověří jeho platnost a jaký limit slíbíme?</p></div></div>
<div class="q"><div class="num">14</div><div><h3>Kdo připraví skutečné provozní údaje?</h3><p>Kdo založí a ověří regionální společnost, sjednotí její název, identifikační a bankovní údaje a schválí ceník i měny? Kdo před spuštěním zajistí ochranu přístupů, omezení veřejného síťového přístupu a výměnu či omezení veřejně dostupných přístupových klíčů?</p></div></div>
<div class="card ink"><h3>Výstupem jednání má být rozhodnutí a odpovědnost</h3><p>U každé otevřené otázky určit zvolenou variantu, vlastníka a to, zda se upraví smluvní text, aplikace, nebo provozní postup. Regionální model společnosti, smlouva o dílo ke každé zakázce a neplátcovství úklidníka jsou již přijaté výchozí volby.</p></div>
<div class="folio"><span>Cleansia · obchodní shrnutí</span><span>8 / 8 · Otázky k odpovědnosti</span></div>
</section>
