# Port panel concept v1

Generated with the built-in image_gen tool. UI mockup, not an implemented Canvas.

References: BlacksmithPanel-57b74f8b.png (interface style), Port_Concept_v1.png (port and ship art).

The Cargo tab combines the current PortPanel and ShipSuppliesPanel functions. Tabs, illustrated inventory rows and the progress bar are concept proposals; existing scripts do not create this layout automatically.

## Prompt

Use case: ui-mockup. Create one highly polished raster concept of the PORT MANAGEMENT PANEL for the medieval RTS Lords of the Valley. This is an interface concept, not a building concept sheet and not a website. Reference image 1 (BlacksmithPanel) defines the established UI style: dark charcoal slightly teal inset surfaces, warm aged timber outer frame, restrained antique gold metal trims, blue enamel buttons, ivory readable Cyrillic serif lettering, tiny low-poly illustrated resource sprites. Reference image 2 (Port building) defines the little port/ship illustration style. Create a distinct maritime identity using a small anchor crest, rope details only at corners, navy accents; keep ornament restrained and functional.

Composition: one complete large portrait-to-nearly-square game panel, front-on orthographic UI, all corners visible with small dark margins, generous spacing, crisp aligned controls, text legible at full image resolution. Approx 1400x1700 composition. No scene behind it, no mockup devices, no branding text other than the title. The interface should occupy almost the entire canvas. Strong coherent hierarchy. One berth only. No prices, ship construction, diplomacy, second berth or trading routes.

Exact visible Russian copy and hierarchy:
TOP HEADER: anchor emblem, large title "ПОРТ", close X at right.
WORKER STRIP: small dockworker portrait, "Рабочие: 2 / 3", distinct blue buttons "Нанять" and "Уволить".
BERTH CARD: small stylized wooden merchant ship vignette at a stone dock, title "Торговый корабль", green dot and "Пришвартован"; short metrics "Экипаж: 2 / 2" and "Припасы: 24 / 30". Small secondary buttons "Назначить корабль" and "Отвязать".
TAB BAR: "Грузы" active in gold, "Экипаж" and "Припасы" inactive, with simple icons. This is a concept of the cargo tab, do not cram all tabs' actions into the same image.
MAIN CARGO AREA: two equal adjacent clearly separated inventory panels with small cargo transfer arrows between them.
LEFT heading "Склад порта", capacity "148 / 300". Three spacious rows, each with a distinct painted 3D-style sprite, resource name and right aligned quantity: stacked logs "Дерево" "80"; iron ingots "Железо" "40"; golden loaves "Хлеб" "28". A selected log row has a subtle gold border. Footer "Свободно: 152".
RIGHT heading "Трюм корабля", capacity "60 / 200". Rows with consistent sprites: "Дерево" "40"; "Железо" "20"; an empty outlined slot. Footer "Свободно: 140". Provisions are separate from trade cargo and must not appear as another 24 food inside this hold.
TRANSFER CONTROLS under inventories: dropdown-style selected item with logs icon "Дерево" and chevron, label "Количество", compact numeric field "40". Large clear primary button with right arrow "Погрузить" and secondary left-arrow button "Разгрузить", plus smaller "Разгрузить всё".
STOCK CONTROL ROW: "Поддерживать запас" with numeric "80", small button "Задать" and small "Сбросить".
BOTTOM ACTIVE OPERATION CARD: title "Текущая операция", log icon, "Погрузка дерева", a gold half-filled progress bar, line "Осталось: 20", a red cancel X button labelled "Отменить". Small quiet explanatory footer "Передача доступна только у причала".

Typography: careful correct Cyrillic, large labels, meaningful button widths, no tiny text walls, no repeated or extra generated labels. Resource icons tactile stylized low-poly, not emojis. Premium game art matching the supplied blacksmith interface, subtle wear and gold edging, no exaggerated fantasy filigree or excessive props. Deliver a finished readable UI concept image.

