# System specification

Ordering system for volunteer fire department festivals.

Written to be read end to end by a human, not by a build tool.

## The source code is the authority on every contract

This document holds the purpose of the product, the decisions that shape it, the reasons behind those
decisions, and the setup checklist the fire department follows. It holds nothing else.

It does not describe routes, request bodies, response bodies, status codes, live update payloads,
database columns, screens or test lists. Those are defined in the code, and the code is the only
place they are defined:

* The entities and their fields live in `backend/GastronomyApp.Core/Entities` and in the initial
  migration under `backend/GastronomyApp.Infrastructure/Migrations`.
* The REST surface lives in `backend/GastronomyApp.Api/Endpoints` with its shapes in
  `backend/GastronomyApp.Api/Contracts`.
* The live update channel lives beside them in the same project.
* The screens and every user-visible string live in `frontend/src`, with the German and English
  wording in the locale files.
* The rules for tests live in the `CLAUDE.md` file of each folder.

An earlier version of this document repeated all of that, and it went out of date twice, because two
places owned one fact. If you want to know what a call returns or what a field is called, read the
source.

## Conventions used in this document

* "Waiter" always means the person carrying orders and trays, never the machine. The machine is
  called "the backend" or "the laptop". In the app that person is called "Kellner" in German and
  "waiter" in English.
* A place where food or drink is made and handed out is a **station**. In German it is always
  "Ausgabestelle", in English always "station". It is called by its own name ("Küche", "Theke innen")
  wherever a specific one is meant.
* The two ways a station can hand its part of an order out are always "zusammen" and "sobald fertig"
  in German, "together" and "as it is ready" in English.
* The three states an item moves through are always "wartet", "in Zubereitung" and "fertig" in
  German, "waiting", "being prepared" and "ready" in English.
* **German uses the Sie form throughout**, on the phones, on the tablets, in the admin pages, in the
  program window and in this printed checklist. A fire department crew says du to each other, but the
  text is read by whoever is holding the device, including people helping out for one evening, and
  Sie is the form that is never wrong for any of them.
* Money is stored and calculated in integer cents. No decimal type appears anywhere.
* Times are stored in UTC and rendered in the laptop's local time zone.
* Names the admin types in (item names, station names, waiter names) are data, not chrome. They are
  stored once in whatever language the fire department uses, and the app does not translate them.
* Two numbers appear on screen, and each is written one way. The global order number is always
  "Bestellung 137" or "Order 137". The per-station sequence number is never shown on its own: the
  station tablet writes both together, as "Bestellung 137, hier Nummer 042" and "Order 137, number
  042 here". No `#` form and no "Nr." form exists anywhere in the product.

Contents:

1. Purpose and scope
2. Where this runs, and what that settles
3. The decisions behind the model
4. Production
5. Numbering
6. Failure behaviour on the phone
7. The program on the laptop and its setup
8. What is deliberately not built, and what is still open

---

## 1. Purpose and scope

### 1.1 What the product does

A waiter walks table to table with their own phone, picks items and quantities, types the table name,
reads the running total aloud so the guest can pay cash, and places the order. The backend splits the
order by station and puts each station's part on that station's tablet. The people at the kitchen and
the bar work that list off exactly as they worked a pile of paper before. A waiter with a free hand
carries the tray out.

The product removes one step from the paper process: the walk from the table to the kitchen. Nothing
else about the process changes. Nobody at a station logs in, nobody is paged, and no screen tells a
waiter to come and fetch anything.

### 1.2 Why it exists

Orders get lost. A paper slip falls behind a fridge, a waiter forgets which table a tray belongs to,
or a slip is written twice and the kitchen produces the same order twice. Every design decision in
this document is answerable to that one problem. Two mechanisms carry the weight:

1. Every part of an order carries a global order number and a number of its own within the station it
   went to. The station's list runs 1, 2, 3, 4, so a missing number is visible to anyone reading the
   list. That is the same check the pile of paper offered, and it needs no screen, no login and no
   software knowledge.
2. Nothing is ever removed from a station's list by the software. An item leaves it because somebody
   at that station marked it ready. A failure is never allowed to look like a success.

### 1.3 Who uses it

| Role | Device | Technical ability assumed | Trained? |
|---|---|---|---|
| Waiter | Their own phone, any browser | None | No. Possibly never saw the tool before tonight. |
| Kitchen and bar staff | One tablet per station, standing at the station | None | No. Three buttons and a list. |
| Admin | The laptop on site | Can follow a printed checklist | Read the setup checklist once |

### 1.4 Scale

Fewer than ten stations. Fewer than ten waiters. A catalog of roughly twenty to sixty items. A few
dozen tables. Peak load is a handful of orders per minute. The system is not designed for scale, and
adding scale is not a goal. It is designed for reliability and for operation by people with little
technical ability.

### 1.5 Non-goals

The following are out of scope. They are listed so that a future change request can be answered with
"that was decided against", not with a redesign.

| Not in scope | Reason |
|---|---|
| Payments of any kind | Cash is handled by hand at the table. The app takes no payment, stores no payment data, and has no card reader. |
| Guest receipts, invoices, tax documents | Any of these would drag German fiscal law (KassenSichV, TSE, GoBD) into a hobby project. Nothing the app shows a guest is a receipt, and nothing is printed at all. |
| Printers of any kind | The fire department chose tablets before any printer was bought. There are no printers, no slips, no print state and no printing hardware anywhere in the product, and none will be added. |
| Notifying a waiter that food is ready | Deliberate, and section 4.5 gives the reasoning. Whoever passes the station takes the tray. |
| Stock, inventory, portion counts | An item is marked sold out by hand, in one tap, by whoever hears that the kitchen has run out. Counting portions is not attempted, because nobody will keep the count correct while serving. |
| Table reservations or floor plans | Tables are moved during the evening. Managing them as objects is more work than the problem is worth. |
| Zones or areas grouping stations | Almost every festival has one kitchen and one bar. Routing comes from the item itself, and section 3.2 describes the one remaining choice a waiter makes. |
| Cloud, remote access, multi-site | There is no internet on site. |
| Accounts, usernames, passwords | Nobody will manage credentials at a festival. Section 2.4 describes what replaces them. |
| Guest self-ordering | The waiter at the table is the product. |
| Reporting and analytics beyond a list of the evening's orders | Nobody will read it. The status change log in section 3.8 exists so that the question "how long did the kitchen actually take" can be answered later from the database, not so that a screen can be built for it now. |
| Tray tracking, delivery confirmation | A waiter carries the tray. The app is not told when it arrives. |
| Cancelling or correcting an order after it is placed | The moment an order is placed it is on a station's tablet and somebody may already be cooking it. Cancelling in software would tell the waiter the order is withdrawn while the kitchen carries on. Section 4.6 says what happens instead. |
| Deleting or cleaning up a mistaken order | It stays in the database. No money moves through the app and nothing aggregates orders, so a wrong order costs a line in a list the treasurer skims once. |

---

## 2. Where this runs, and what that settles

This shapes almost every other decision in the document, so it is stated once here and assumed
everywhere afterwards.

### 2.1 The site, the laptop and the WiFi

The server runs on **a random laptop on site**, operated by people with little technical ability.
There is no cloud, no internet on site, no IT support, and often nobody present who has seen the tool
before.

The site network is **WiFi only**, set up by the fire department themselves. The devices have to be
able to reach each other, which means client isolation in the router has to be switched off. That is
a documented step in the setup checklist rather than something the software can arrange.

### 2.2 Plain HTTP, and everything that follows from it

Transport is **plain HTTP, not HTTPS**. A self-signed certificate produces a browser warning in front
of exactly the people who cannot judge it, and it does not restore secure-context features anyway. So
the scheme, the bind address and the port live in one place in the configuration, which keeps a later
move to HTTPS a setting rather than a rewrite.

Plain HTTP means there is no secure context, and three things follow from that directly.

**There is no service worker and no installed app.** A device is online-only, and nothing in this
product runs while its page is closed. Section 6 is entirely about what that means when a submission
fails.

**The web app can never open the camera.** Scanning the setup code happens in the device's own camera
app, which opens the URL in the browser. An in-page scanner is not a feature that was skipped: the
browser interface for reading a camera requires a secure context, so it is a feature that cannot
exist here.

**Anything readable off the air is readable by anybody in the marquee.** That is why there is no
admin password anywhere (section 2.5).

### 2.3 The address, and why the QR code carries it

The laptop's address is not stable and cannot be made stable without administrator rights on the
router. **The QR code carries the full URL including the current address**, which is why one picture
solves both problems at once: it is the credential and it is the address. Nothing in the product ever
depends on a phone remembering where the laptop is.

The laptop works out which of its own addresses a phone can actually reach, and it does that in one
place in the backend that the program window's own network check also calls. The part worth writing
down is why it cannot simply take the first address it finds: every Windows laptop carries two or
three link-local addresses on virtual adapters, and the operating system does not list them in a
stable order. Without the filtering the QR code would carry a dead address on some starts and a
working one on others, which is the least diagnosable failure in the whole product. When no usable
address is left the laptop falls back to its own loopback address, which is honest: no phone can
reach it, and the operator is told there is no network.

**The address in that QR code is the one the devices hold, and nothing in the product survives it
changing.** A device's stored token lives in the browser's storage for that exact origin, so a router
reboot that hands the laptop a new address leaves every device holding a token it cannot reach. There
is no software recovery for that. It is why the setup checklist makes a fixed address a step rather
than a hope, and why the admin overview names the previous address when it has changed. The recovery,
when it happens anyway, is that every device is set up again from the new address, one at a time.

### 2.4 No usernames and no passwords

There are no usernames and no passwords anywhere in the product.

**Every device is set up from the laptop, one at a time, with one QR code.** The admin puts the
waiter on the list or picks the station, creates the code for that owner by name, and the person
holding the phone or the tablet scans it with their own camera app. The browser that opens finishes
the job and asks for nothing, because the invitation already says whose device this is.

The code is single-use and short-lived. It is exchanged once for a long-lived device token that lives
in the browser's storage, and every device can be signed out again from the admin pages. Section 3.4
covers what that means for the person holding the device.

### 2.5 The admin pages are reachable from the laptop only

The admin part of the backend answers the machine it is running on and nothing else.

That replaces a credential nobody would manage with a physical constraint everybody understands: the
admin is the person standing at the laptop. On an open WiFi with plain HTTP, any admin login would be
readable off the air, so the physical constraint is genuinely stronger than the alternative and not
merely simpler. The laptop's own network addresses count as the laptop, not only its loopback
address, because otherwise the volunteer who reads the address off the overview screen and types it
into the laptop's own browser meets a program that looks broken.

The cost is accepted and is worth stating: **marking an item sold out is a walk to the laptop.**

**The admin page itself is served everywhere; the admin data behind it is not.** A phone that opens
the admin address gets one sentence telling the reader to open the admin pages on the laptop. Serving
a broken admin screen with no explanation to a curious waiter is worse than either extreme.

### 2.6 How the devices are kept up to date

The laptop pushes live updates to the devices over one connection, so a tablet gains an order without
anybody refreshing it and a phone learns that an item just sold out while the waiter is still
standing at the table.

**A client is told that something changed and then asks for the screen it is drawing. It never
renders from the message it received.** That is a rule rather than an implementation preference. It
keeps one producer for every derived figure, and it means an update missed during a reconnect costs
nothing, because the next fetch is complete anyway.

Three consequences are worth having written down.

* **The push channel is never a source of truth.** After any reconnect the client fetches again
  rather than assuming it missed nothing.
* **Nothing in the ordering path depends on it.** If the live connection never comes up at all,
  orders still go through.
* **A tablet only ever hears about its own station.** A tablet that redrew on another station's
  traffic would be doing nothing with it.

Signing a device out takes its connection away in the same breath as its token. The message the
device receives is what tells the person holding it what happened; it is not what enforces the
revocation.

### 2.7 Messages travel as keys, not as sentences

When the backend refuses something, the answer carries a message key and its parameters, and the
device renders the sentence from its own resource file in its own language.

A device has a language stored on it, which an HTTP header does not know about, and a message that
exists both as a backend string and as a frontend string will drift the first time one of the two is
edited. Technical detail is added only for the admin: a waiter's phone never receives a stack trace
or a socket error string.

**A check the screens already prevent gets one sentence, not five.** An order with no items, an order
with no table name, a price below zero, a line with no station where the waiter had to pick one, and
an item the admin never gave a station: the laptop refuses all five, because bad data must never be
stored, and the phone and the admin pages make all five impossible to produce. They share one message,
which says that the order could not be processed and asks the waiter to take it to the station in
person. Which of the five fired, and the item it names, goes into the laptop's log, where a developer
can find it and a volunteer never has to read it. Writing five separate sentences would ask the person
holding the phone to fix something the screen never let them get wrong.

---

## 3. The decisions behind the model

### 3.1 The festival, and what belongs to it

A festival is a named period with a start and an end, and it is the thing everything a waiter or a
station sees belongs to. The fire department runs the same festivals again and again, at different
places, with different stands and different goods, so the menu, the prices and the stations are facts
about one festival rather than about the program.

**The phones and the tablets follow whichever festival is running right now.** Running means the
festival is not hidden and the current moment is at or after its start and before its end. Nobody
picks a festival on a phone and no screen has a switch for it: the laptop works it out from the clock
every time it is asked. When no festival is running the menu is simply empty, the open items list is
empty, and a station tablet says that no festival is active. That is not an error state, it is what a
Tuesday afternoon in March looks like.

**The period is padded well beyond the real opening hours**, and that is the intended way to use it.
A festival that runs from five in the afternoon until three in the morning is entered as noon until
three the following afternoon. Nobody has to be at the laptop when it starts or ends, and nothing in
the program treats the boundary as a special moment.

**Stations and items are created once and reused.** A station belongs to a festival by being added to
it, and an item goes onto a festival's menu with the price it costs there. Last year's price survives
this year's, because the two prices sit on two different festivals. Two places that both have a
kitchen are two stations, each named for its place, rather than one station that means something
different depending on the weekend. Waiters are never tied to a festival: a waiter works at whichever
one is running.

**A festival can be copied**, and that is how a repeat location is set up. The copy starts with the
same stations, the same items and the same prices, and the person at the laptop types only the name,
the start and the end, then corrects the few prices that changed. The copy has no orders and its
numbering starts at one.

**A festival is hidden rather than deleted.** Hiding takes it out of the list without touching the
orders that were placed at it, and a festival that is running cannot be hidden, because that would
empty every phone and every tablet in the middle of service. A hidden festival can always be shown
again, because no two festivals ever cover the same period in the first place. The laptop refuses a
start and an end that run into another festival, and it counts the hidden ones too, so hiding a
festival never frees its dates for a second one.

### 3.2 Stations, and how an item finds one

A station is a kitchen or a bar. There is no grouping above it: a station is the whole of the site
structure the system models.

Each catalog item names the stations that are capable of producing it. Bratwurst is assigned to the
kitchen. Beer at a site with two bars is assigned to both. From that, one rule, and it is the only
routing rule in the system:

1. **One capable station.** The item goes there. The waiter is never asked and no station control is
   drawn for that item. This is the normal case at a site with one kitchen and one bar, and it is
   completely invisible.
2. **More than one.** The waiter chooses, on the phone, at the moment the item is added. The choice
   is visible on the item in the summary and can be changed until the order is sent.

**The choice belongs to the one item.** It is not a session setting, not a device setting and not a
shift setting, and nothing about it is remembered for the next item or the next order. A waiter
carrying one tray to the marquee and the next to the terrace would otherwise be fighting a setting
they never set.

**An item on a festival's menu can never be left with no station**, which is what keeps that rule
free of a "nothing" case. The admin cannot put an item on a menu without naming at least one station
of that festival, a station cannot be taken off a festival while it is the only one preparing
something on that festival's menu, and a station cannot be switched off while it is the last active
station of any item still on the menu of a festival that has not finished. The refusal names the items rather than simply
saying no. A station also cannot be switched off while it still has unfinished work, and that refusal
names how many. Because of those two rules there is no fallback station and no default station
anywhere in the system.

The rule lives in one place and is used by order submission, by the phone's own preview and by the
tests. It is not reimplemented anywhere.

### 3.3 A table is free text, with suggestions

**A table is a name typed onto the order, not an entity that orders point to.** There is no table
list to maintain.

At a festival the tables are beer benches. They get moved, added and joined together during the
evening, and guests sit at whatever is standing. If an order needed a table from a list, then the
first table that is not in the list blocks an order, and blocking an order is the exact failure this
product exists to prevent. Free text can never block.

**The suggestions come from the table names already typed on recent orders.** The table field is a
combobox: the waiter types freely, and the names already in use sit in its dropdown. That is what
keeps spellings consistent, and it needs no admin screen and no entity of its own. Reading only the
recent orders is what stops the list growing without a bound over a long festival.

**The stored name is not normalised.** "Tisch 12", "tisch 12" and "T12" stay three different tables,
because grouping on the exact string is the rule a volunteer can predict, and normalising is code
that is easy to get subtly wrong. The dropdown is what prevents the typo in the first place, at the
moment it would be made.

The open items screen groups on that exact name, so a table settles together rather than order by
order.

### 3.4 On the menu, and sold out, are two different things

An item carries two separate flags, they are set by two different people at two different times, and
the product never uses one word for both.

| | Deactivated ("deaktiviert") | Sold out ("ausverkauft") |
|---|---|---|
| What it means | The item is not in use at all, at any festival | The item is on this festival's menu and has run out tonight |
| Who sets it | The admin, at the laptop, setting up the event | Whoever hears that the kitchen has run out |
| When | Before the event, between events | During service, and very often reversed twenty minutes later when somebody finds another crate |
| How | The item editor, with a confirmation, because it is a considered edit | One toggle in the item list, one tap each way, no form and no dialog |
| On the phone | The item is not in the catalog at all | The item stays in the list, greyed, not selectable, with "Ausverkauft" underneath |

Sold out is reversed constantly by somebody who is busy, so it is one tap and nothing else. That is a
design requirement rather than a layout note, and no screen and no part of the backend may treat the
two flags as one concept.

**Marking an item sold out is allowed at any time and is never refused**, in either direction. It
changes nothing about orders that already exist.

**A sold-out item stays on the phone rather than disappearing from it.** An item that vanishes
silently sends a waiter hunting through categories for something that was there a minute ago,
wondering whether they are on the wrong screen. Greyed out with "Ausverkauft" underneath answers that
question at the moment it is asked, which is the moment the guest asks for it.

**An item that sold out, or was taken off the menu, after the phone last read the catalog is still
accepted when the order arrives.** The guest ordered it, the waiter read the total aloud, and the
cash may already be in their apron. Only an item the laptop has never heard of is refused, and that
is a broken client rather than a guest.

**The phone, on the other hand, will not send such an order until the waiter has taken those items
off it.** The live update reaches the open basket and flags the item while the waiter is still
standing at the table, so they learn before they send rather than after. Both ways of sending are
held back while an order carries a sold-out item or one that has left the menu, a line under the
buttons says what has to go, and one button takes all of those lines off at once. The waiter then
offers the guest something else, which is the conversation the flag exists to start.

**A line whose station no longer prepares its item is the third case of that same family.** A drink
that two bars could pour carries the bar the waiter picked, and an admin who unticks that bar, or who
moves an item from the kitchen to a bar, leaves the line pointing at a station that no longer makes
it. The phone marks that line the way it marks the other two, the same button clears it, and the
waiter adds the item again, which routes it to the station that is left without asking anybody
anything. The laptop refuses such an order too, but the phone has usually caught it first, so that
refusal only ever reaches a phone whose menu was out of date.

**The laptop is deliberately not part of that check.** An order refused by the laptop would arrive
after the waiter has taken the cash, and handing money back in front of a guest is worse than a
station being asked for something it has run out of. The block lives on the phone, before the money
changes hands, and the laptop goes on accepting whatever reaches it.

### 3.5 One device per owner

**A waiter has at most one phone, and a station has at most one tablet.** The owner points at the
device rather than the other way round, because the question the product actually asks is "which
device belongs to this person or this station".

**Setting a device up again replaces the previous one, and the old token stops working at that
moment.** That one rule covers the three situations that occur. A phone is lost, and whoever finds it
must not be able to send orders to the kitchen. A battery dies and the waiter borrows a colleague's
handset for the rest of the evening. A tablet is swapped for a charged one halfway through. Without
the rule the lost phone keeps working all evening, and no amount of admin diligence at 22:00 makes up
for that. It is also why the replacement happens when the new code is created rather than when the
new device finishes scanning it.

**A browser holds one setup at a time.** As it scans, the phone or the tablet hands the setup it is
already holding back to the laptop, and the laptop signs that one out the moment the new one is
finished. Scanning a code that belongs to somebody else is therefore a handover rather than a second
setup: the waiter or the station the device was set up for a minute earlier goes back to having no
device, and the connection the browser had open under that old name is closed with it. Without that,
one browser would count as two devices at once, the laptop would go on saying that the person it left
behind is set up, and nobody standing at the laptop could see that it is not. A device that hands over
nothing, because it was never set up or its storage was cleared, and a device that hands over a setup
the laptop has long since removed, are both ordinary: the scan finishes exactly as it otherwise would
and nothing is said about it.

**An order belongs to a person, not to a phone.** That is what makes a flat battery, a signed out
phone or a fresh setup survivable: the evening's history follows the human, so a waiter who finishes
the evening on a borrowed handset keeps the orders they took.

**A tablet never says which station it is.** Its token already does, so there is no way to ask for
another station's work and no id to mistype.

**A station is named at the laptop, a new waiter names themselves on the phone.** The admin creates
an Ausgabestelle and names it while setting the event up, long before its tablet is needed, so that
code names the station and the tablet asks nothing. A waiter may be added minutes before service
starts, so the code for somebody new names nobody, and the waiter types their own name once they
have scanned it. That typing is what puts them on the list. A code for a waiter who is already on
the list, which is what setting their phone up again issues, names them and asks nothing either.

**The admin can rename a person at any time, and that is a safety valve rather than a convenience.**
The name on an order is whatever was typed into the waiter list, so sooner or later somebody is put
down as "Papa" or as a nickname the kitchen does not know. Renaming changes the row and not its
identity: the person keeps their orders, and the new name counts from the next order onwards.

**The setup code lives for five minutes and for one device.** Five minutes is long enough to walk
from the laptop to wherever the tablet is standing and unlock it, and short enough that a code
somebody photographed over a shoulder is dead before they could use it. At most one code is
outstanding at a time, so two admin tabs clicking within a second produce one code and one loser, and
redeeming one is a single indivisible step so that a photographed code cannot set up a second device.

**The picture is shown once and is never fetchable again.** The code behind it is stored only as a
hash, because keeping the code itself beside its own hash would make the hashing decorative. What a
reload costs is therefore the code, not the invitation: the admin creates another one, which is one
click. Nothing has to stay open in the meantime, and when the laptop cannot draw the picture at all
the panel says so in a sentence and offers a new code, rather than showing a broken image.

**Language is a setting on the device.** A waiter whose phone is set to English gets English from the
app and in every message the laptop sends them. The station page carries its own visible language
switch, because a tablet is set up once and then stands at a station all evening, and the person who
works it may not be the person who set it up.

### 3.6 An order keeps what the guest was told

**Every item on an order carries the name and the price as they stood when the order was taken.**
Editing an item in the admin never changes an order that already exists.

**The price stored is the price the phone displayed**, and it is stored untouched rather than looked
up again when the order arrives. That is deliberate: the guest was quoted that price at the table and
the cash may already be counted against it, so the figure the evening is settled on is the figure the
waiter read out. The order's total is the sum of those stored prices, so a phone holding a stale
catalog produces a figure on a screen that matches the figure in the database rather than one that
quietly disagrees with it.

**An accepted order is immutable apart from where its items are in production and whether they have
been settled.** Items are never added, removed, renamed or repriced, and an order is never cancelled
or deleted.

**One item row is one physical portion. There is no quantity: three beers are three rows.** Rows
carrying the same item name and the same note are counted together when a screen is drawn, so the
reader still sees "3 x Bier" rather than three repeated lines. The reason for the shape is that a
portion is what moves through production: three beers can be in three different states, and a
quantity column cannot hold that.

**An order has no stored status and no stored total.** Both are worked out from the items on every
read, so the two can never disagree with each other or with the rows underneath. Storing either would
be a second place for the same fact to live, and the first half-written transaction would leave them
contradicting each other.

### 3.7 Settlement is recorded per item

**No money changes hands in the app.** It shows prices so the waiter can add up, and it records
whether items have been settled so the people running the stand can see what a table still owes. It
takes no payment, handles no cash, and never issues a receipt. Settlement is a note for the people
running the stand, not an accounting record and not a till.

**Payment is tracked per item, because a table often pays for only part of what is open**, and
because food and drink are sometimes given away, for example to the band playing at the festival.
Everything above the item is derived and never stored: an order is fully settled when all of its
items are, a table's open amount is the sum over its unsettled items, and what was given away is the
difference between what an item cost and what was collected for it. A table that pays only part of
its tab therefore shows the rest in that same figure, because from the stand's side the two are the
same thing: money that was never collected. The phone names that figure under each table as what was
not collected in the last twenty four hours, wording that covers the giveaway and the short payment
alike. Calling it a giveaway would be a lie about the table that paid part of its tab.

**A settled item is never settled a second time.** A double tap cannot double count and cannot
overwrite the reason somebody typed earlier. When part of a selection had already been settled by
somebody else, the phone says so and names how many, so the waiter can check whether they collected
that cash twice.

**One settlement covers one table and never two.** As soon as a line of a table is ticked, every
other table on the screen is held back: its lines cannot be ticked, its box for the whole table is
dead, and it stays that way until the selection is empty again. The laptop refuses a selection
spanning two tables as well, and says only that the settlement could not be processed, because a
waiter working from the screen can never produce one. A settlement that did span two tables would
record the second table as paid in full and take it off the list, and nobody would ever ask that
table for its money.

**A settlement whose answer never arrived is not a settlement that failed.** The phone waits ten
seconds and then stops waiting, which tells it nothing about what the laptop did with the items. It
says so in those words and sends the waiter to look at the list, rather than claiming that nothing
was settled and inviting a second attempt that the laptop would answer as a double tap. A refusal
that did arrive is knowledge and keeps saying what it says. The list fetches itself again whenever
the laptop reports a settlement, and beside the heading sits a control that fetches it on demand,
which is the one thing a waiter has to do after an answer that never came.

**Settling means naming the amount the table handed over.** There is one settle action and it takes a
number. The full price of the selected lines is the ordinary case and the screen offers it ready to
send. Nothing at all is the item that was given away, to the band for instance. Everything between
the two is the table that pays twenty euro off a two hundred euro tab, which happens often enough at
a festival that it needs an answer of its own instead of a waiter settling one line and leaving the
rest standing. More than the price is allowed too, because a guest who rounds up should not have to
be argued with.

**The amount is spread over the selected lines by what each one costs.** A line that already carried
a settlement is left exactly as it was and takes no part of the money. The cents that do not divide
evenly go to the first lines in the selection, so the shares always add back up to the amount that
was typed, to the cent. When every selected line is priced at nothing, the amount is split equally
instead.

**An amount below what the selection costs always carries a typed reason.** The confirm button stays
disabled until one is typed, so nothing leaves a table short without a note somebody can read the
next morning, and the reason stays readable on the table afterwards rather than only being stored.
Paying the full price or more needs no reason, because there is nothing to explain.

**The phone keeps the ordinary case at one tap.** Settling at the full price is a button of its own
and asks nothing further, because that is what most tables do. Beside it sits a second button for any
other amount, which opens a screen asking what the table handed over. That screen names what the
selection costs, offers the same figure in the amount field ready to be overwritten, and asks for a
reason as soon as the typed amount falls below it. While the amount covers the price there is no
reason field at all, and while a short amount has no reason beside it the confirming button does
nothing.

**The two send buttons exist because the two cases are decided at the table.** Sending the order
plainly leaves every item open for the table to settle later. Sending it settled marks every item as
paid at its displayed price, which is the guest who pays on the spot. The order that reaches the
stations is identical either way. Which of the two it is gets confirmed before the order goes out,
because the choice decides whether anybody still expects cash at that table.

**Who took the order and who collected the money are two different people often enough that both are
recorded.** The second one is stored and nothing more: no screen shows it today. It is there for the
takings-per-waiter figures the fire department will want after the festival.

### 3.8 The status change log

Every time an item's production status changes, one row is appended to a log, in the same transaction
that writes the new status. Nothing in the log is ever updated or deleted.

**It exists so that the fire department can find out afterwards how long each step took**, which is
the question somebody always asks the morning after: how long did a Bratwurst really take when it was
busy, and how long did trays stand at the bar before somebody carried them out. Nothing in the
running system reads it.

**It is never read to decide the current state.** The current state is the status on the item and
only that. A log that is also a source of truth is a second place where the truth lives, and the two
would disagree the first time a write half succeeded.

---

## 4. Production

### 4.1 Delivery mode

Before sending, the waiter chooses per station the order touches whether that station hands its
part of the order out **together**, or **as each item is ready**.

| | Together | As it is ready |
|---|---|---|
| German | Gesammelt ausgeben | Einzeln ausgeben |
| English | Hand out together | Hand out item by item |
| What it means | The station keeps the part back until its last item is ready | The station passes each item across as soon as that item is ready |
| When it is right | A family eating together | A round of drinks, or a table that is happy to be served in waves |

**Together is the default**, because a table sitting down to eat is the ordinary case and a waiter who
answers nothing gets the answer that surprises nobody.

**The choice is fixed once the order is sent.** No screen changes it afterwards: not the phone, not
the tablet, not the admin. The station has already arranged its work around the answer by the time
anybody would want to change it, and a mode that can be flipped underneath a cook is a way to lose
half a tray.

A station that was not part of the order is not asked about. An order that touches two stations is
asked twice, once per station, and the two answers are independent: the kitchen may hold the food
back while the bar sends the drinks out as they are poured.

### 4.2 Production status

Every item moves through three states, in one direction only.

| State | German | English | Meaning |
|---|---|---|---|
| Waiting | wartet | waiting | Nobody has started it |
| Being prepared | in Zubereitung | being prepared | Somebody at the station is making it |
| Ready | fertig | ready | It is made and can go out |

**Ready is final.** There is no way back from it, on any screen, for anybody. Marking something ready
tells a waiter to carry it out, and a state that can be taken back would be the software telling a
human something untrue about a tray that has already left. An item marked ready by mistake is
answered the way the paper process answered it: somebody says so at the station.

A request to move an item backwards is refused, and the refusal tells the reader to reload the page
because the item is already further along than their screen shows.

**Staff can advance one item or a whole order at once.** A station with eight beers on one order taps
once rather than eight times. **The step is all or nothing**: if a single item in the selection
cannot take it, nothing at all is written, so a card is never left half moved.

### 4.3 What a station sees

The tablet standing at a station shows that station's work and nothing else. It is the screen that
replaced the pile of paper.

**The two delivery modes are separated on it, because the station does two different things with
them.** Whole orders that go out together stand on one side, and the single items that go out as they
are ready stand on the other. Oldest first in both.

**An item that is already ready stays on a card that goes out together**, marked as ready, because
the card is the unit of work and the person reading it needs to see what is done and what is not. An
item that goes out on its own leaves the screen the moment it is ready, because that is when it
leaves the station.

**A card leaves the screen when every one of its items is ready, and nothing else removes anything.**
The sequence number in the header of each card is what makes a missing one visible, which is the
whole loss detection mechanism of the product and the reason the card is drawn the way it is
(section 5).

**A refused change never leaves the tablet showing something that did not happen.** Every failure
carries a sentence the tablet renders, and every one of them tells the reader what to do next: reload
the page, tap an item, or tap again because the laptop could not be reached and nothing was changed.

### 4.4 Estimates

Each catalog item may carry a production time in minutes. From those the phone shows the waiter,
before the order is sent, roughly how long the guest will be waiting.

* **The backend reports, per station, the minutes currently queued**: the production minutes of that
  station's unfinished items, added up, with a missing production time counting as zero. A station
  with nothing waiting reports zero.
* **The phone adds the item's own minutes** to its station's queued minutes, and that is the estimate
  it shows for the item. An item that more than one station could produce is shown the shortest of
  those stations' answers, because that is the one the waiter would pick.
* **On the summary the estimate stands in parentheses behind the item**, on the same line as the
  count and the name, so a line reads "5 x Bier (~34 Min.)" and the price keeps the right-hand
  edge to itself. The tilde says the number is a rough one, and an item with nothing in front of it
  reads "(~0 Min.)". An item whose production time was never filled in has no estimate to show at
  all, and so does a line that has not been given its station yet: both carry the count and the name
  alone rather than a placeholder or a guess.
* **A part of an order sent together is ready when its slowest item is ready**, so the figure for the
  whole part is the largest of its items' estimates. That figure is written on the button that
  chooses the collected handout, which then reads "Gesammelt ausgeben (~40 Min.)", so the
  choice and the waiting it costs stand in one place.
* **A part sent as it is ready has no single estimate**, and its button carries none. Its items leave
  the station one at a time, so a number for the whole part would be answering a question nobody
  asked. Each item still carries its own.

**Estimates are computed, never stored.** Nothing in the database holds a predicted time, nothing
compares a prediction against what happened, and no screen reports on the accuracy of an estimate. It
is an aid for the sentence "das dauert etwa zwanzig Minuten" at the table, and nothing else depends
on it.

**A missing production time counts as zero rather than blocking the estimate.** Most drinks will
never have one filled in, and an estimate that refuses to appear because somebody left a field empty
is worse than an estimate that is a little optimistic. The admin form says so: leave the field empty
for items that are handed over right away.

On an item row the estimate rides inside the item's own name, in parentheses behind it, written as
short as it can be read: "Wasser (~6 Min.)" in German and "Wasser (~6 min)" in English. The row also
has to carry the price on a phone held in one hand, and the tilde says the number is a rough one. An
item with no production time of its own carries no estimate at all, so the parentheses never appear
for a drink that is simply handed over.

**If the estimates cannot be loaded the order can still be sent.** The phone says so once and every
other control on the screen carries on working. An estimate is decoration on the ordering path and is
never allowed to stand in front of an order.

### 4.5 Nobody is notified

When an item is marked ready, the tablet shows the table name and asks somebody to write it on the
tray. The tray then stands there.

**No waiter is called, no phone buzzes, and no screen anywhere says that a tray is waiting.**
Whichever waiter next passes the station picks the tray up and takes it to the table written on it.

This is deliberate and it is worth saying plainly, because it looks like a missing feature and it is
not. A festival marquee is loud, phones are in aprons, and a waiter is usually mid-conversation with
a table. A notification that cannot be relied on is worse than none, because it teaches everybody to
stop walking past the station. The process the fire department already runs is that people pass the
hatch constantly, and this product is not trying to replace that.

### 4.6 A guest changes their mind

This happens several times an evening, and the product's answer is deliberately not a button.

**Before the order is sent** there is nothing to specify. The order is a cart on the phone. Removing
an item and starting over are ordinary editing, and the backend has never heard of the order.

**After the order is sent there is no cancel action, anywhere, for anybody.** The moment the order is
accepted it is on the station's tablet and somebody may already have started it. A cancel button
would clear the row on the waiter's phone, which reads as "that order is withdrawn", while the
kitchen goes on cooking. That is a system telling a human something untrue about the physical world,
which is the one defect this entire document is written against.

**What happens instead** is what happened before there was any software: the waiter walks over and
tells the station. That walk is short, it is certain, and it is the same walk any workable design
would have required. The extra order stays in the database, which costs nothing: no money moves
through the app, and the item can be settled with nothing paid and a typed reason if it was made and
given away.

---

## 5. Numbering

Two numbers appear on the station tablet. Both matter, and they do different jobs.

* The **global order number** is how a person says one order out loud across the whole site.
  "Bestellung 137, wo ist das Bier dazu." It is shared by every station's part of that order.
* The **per-station sequence number** is how a person sees at a glance that something is missing. The
  kitchen's cards run 1, 2, 3, 4. If the list jumps from 41 to 43, then 42 is missing and the station
  knows it without touching software.

The tablet writes them together, as "Bestellung 137, hier Nummer 042" and "Order 137, number 042
here". The sequence number is padded to three digits, so the rows stay the same visual width all
evening and a jump is obvious in a list.

### 5.1 Why gaps mean what they mean

A gap in a station's list has to mean "something is missing", otherwise the station learns to ignore
gaps and the mechanism is dead. Three rules keep that true, and they are the reason the counters are
built the way they are rather than in any simpler way:

* **A number is allocated only by a transaction that commits**, inside the same transaction that
  accepts the order. If anything in the acceptance path fails, the whole thing rolls back and the
  counter rolls back with it. There is no separate "get a number" call that can succeed while the
  order fails.
* **Counters live in the database, never in memory.** A restart, a crash, or a laptop that lost power
  mid-evening resumes at the exact next value. There is no in-process cache and no batch reservation,
  because both hand out numbers that may never be used.
* **Nothing is ever deleted or cancelled.** A committed number always has an order behind it.

So every gap in a station's list means one thing and only one thing: that part of an order did not
arrive, and the admin order list can name which order it belonged to in five seconds.

### 5.2 Every festival counts from one

Both counters belong to the festival. Its first order is number 1, and at every station of that
festival the first slice is number 1 as well. A second festival starts at one again without anybody
doing anything, and the orders of the festival before it keep the numbers they were given.

There is nothing to reset and no button to press, which is the point: the one thing a volunteer could
forget on the evening has been taken away. A station that is added to a festival part way through
starts at one there, and taking a station off a festival is refused once it has been sent any work,
because a station that came back would repeat numbers its tablet had already shown.

---

## 6. Failure behaviour on the phone

### 6.1 What is possible and what is not

Plain HTTP means no secure context, which means no service worker and no installed app (section 2.2).
A device is online-only, and nothing in this product runs while its page is closed.

That single fact decides the design of this whole section. **There is no background queue, no retry
timer, no window after which an order is given up on, and no data structure holding orders that are
waiting to be sent.** A queue that only runs while somebody is looking at the screen is not a queue,
it is a timer with a promise attached, and a volunteer who reads "5 Bestellungen warten auf die
Verbindung" and puts the phone in their apron has been told something the product cannot keep.

The single time limit anywhere in the product is the ten seconds an attempt may run before the phone
stops waiting for an answer, described in section 6.4. It ends a request a person started, it leaves
the order standing on the screen, and it sends nothing itself.

What exists instead is smaller and true: the order stays on the screen, and the waiter taps a button.

### 6.2 The draft cart, and why it is not a queue

The order being built is written to the browser's storage on every change: an item added, an item
removed, a station chosen, a note typed, the table entered, a delivery mode picked. Its only job is
that a reload does not lose a half-built order. That storage survives a tab being closed, a browser
being killed and a phone rebooting, which is why the device token lives there too. On load the app
puts the order back on the screen exactly as it was, and a draft that cannot be read is reported
rather than silently discarded.

**A line holds only what the waiter decided: the item, the note, and the station it goes to.** The
name and the price come from the item list the laptop pushes out, and that list is the only place
either one is ever read, on the screen and in the order that travels back to the laptop alike.
Prices are printed on the paper the waiters carry and do not move during an evening, so a second
copy of a price on the line would only be a second version of a number that already exists.

A line keeps two names of its own, and each of them is read only when the item list can no longer
supply it. The first is the name the item had when it was added, read when the item has left the
menu, so nothing else can name it any more. Such a line renders greyed and says so, and it shows no
price, because there is no longer a price to show. It adds nothing to the total, and it is sent as
costing nothing, so the total the waiter reads out and the total the laptop would record are the
same figure. The line is not dropped: the summary offers
one button that removes every line the guest can no longer be given, whether the item sold out or
left the menu. **The app never removes such a line by itself**, because the guest ordered something
and the waiter needs to see what falls away in order to offer them something else.

The second is the name the station had when it was chosen for that line, written down whether the
waiter picked the station or the item goes to one station only and nobody was asked. The item list
carries the stations that are switched on, so an admin who switches one off in the middle of service
leaves the phone holding lines routed to a station it can no longer name. The summary groups an order
by station and heads each card with that name, and asks under the card how that station is to hand
its part out, so without the kept name the waiter would read a heading naming nobody and a question
with a hole in it. While the item list still carries the station its current name wins, so a station
renamed during the evening reads new on the summary rather than as it was when the line was added.

**This is a draft cart and not a queue, and the distinction is load-bearing.** A draft cart holds one
order, the one on the screen, and nothing ever sends it except a person tapping the send button. It
has no retry loop, no ordering, no head and no ages. Beside the order the storage keeps one small
record, and only because of the freeze in section 6.4: what became of the last press of send, how
many attempts have been made, whether any attempt was left unanswered, whether the waiter chose to
settle, and the message that stands on the screen. It exists so that a reload cannot hand back an order that looks untouched while the laptop
may already be holding it. That record describes one press of one button and can never describe a
backlog. Any implementation that gives the storage a list, a timer, or anything at all that sends by
itself has rebuilt the queue this section deliberately removed, and it should be rejected in review
no matter what it is called.

The draft is cleared when the order is accepted. The one other thing that clears it is the waiter
saying, on the dialog described in section 6.4, that an order the phone has no way left to send is
written down on paper.

### 6.3 The submission id

Every order carries an id the phone generates **once**, at the moment the order comes into being,
long before anybody has pressed anything. Send uses it, and so does every retry. It is regenerated by
nothing: not a retry, not a reload, not setting the phone up again. A new id is created only when the
next order is started, so an order and its identity are always the same age. Generating it at the
press would have let the id double as a note of whether the order had ever been sent, and that job
belongs to the record in section 6.2, where a reload can still read it.

**This id is the whole reason a manual retry is safe, and the case it covers is the one a volunteer
cannot see.** If the first submission reached the backend and its answer was lost coming back, then
an order exists, with numbers, already on the stations' tablets, while the phone shows a failure. The
waiter taps retry, because that is what the screen told them to do. Without the id the backend would
create a second order and the table would get everything twice. Two identical orders in one evening
is the second worst outcome in this system, and it would be produced by the honest behaviour of a
waiter following an instruction.

With the id, the second submission finds the order that already exists and returns it, unchanged,
with its original numbers. **The phone shows that answer exactly as it shows a first-time success**,
with the same wording and the same order number, because to the waiter it is the same event: the
order arrived. A screen that distinguished the two would be describing the network rather than the
order.

**The id alone decides that two requests are the same order.** The backend never compares what the
second request carries against what it already stored: a request whose id it recognises is answered
with the order it holds, whatever else that request says. Comparing the two would mean guessing at the
difference between a retry and an edit, and the phone has already settled that question. An order the
laptop may be holding is frozen from the press of send until the phone knows what became of it, so the
id can only meet a stored order again as an unchanged retry. An order the laptop answered no to was
never stored, so the waiter is free to put it right and send it again under the same id, and the
laptop takes it as the first order it has seen under that id.

### 6.4 The specific failure cases

**WiFi drops while the waiter is still picking items.** Nothing happens. The basket is local. The
header shows the reconnect line so the waiter is not surprised at the moment they send.

**WiFi drops after tapping send.** The request fails, the order stays on the screen in full, and the
retry stands in the strip along the bottom where the two send buttons were. The waiter can walk ten
metres towards the marquee and tap again.

**The request left and the answer never came.** The dangerous one, and section 6.3 is entirely about
it. The retry carries the same id, so exactly one order exists either way.

**The laptop answered and its disk did not.** The order was not stored, and the message says the
laptop could not save the order rather than saying it could not be reached, because a message that
states the wrong cause sends somebody off to check the WiFi. An order no attempt was ever left
unanswered on does not freeze, so the message asks for it to be sent again without naming a button:
both ways of sending are back on the screen and the waiter picks the one they meant.

**The laptop answered, and the answer was no.** Two reasons have wording of their own, because they
are the two a waiter can put right at the table: an item that is no longer on the menu, and a station
that does not prepare the item on the line. Both of them are the menu changing under somebody
mid-order, both name the item, and both name the tap that clears it. Every other reason is something
the screens already prevent, and those share the one sentence from section 2.7 while the log keeps
which of them it was. Nothing was stored, and the answer says so, which leaves the
waiter nothing to wonder about. An order no attempt was ever left unanswered on does not freeze. The
reason stands on the summary in their
own language, every control comes back, the items screen lets them in again, and they put right what
the reason names and send once more. That attempt carries the same submission id, because an order
which was never stored gives the laptop nothing to recognise later, and keeping the id is what makes
the attempt safe if it is the one that goes unanswered. The reason leaves the screen as soon as the
waiter changes anything, since it described the order as it stood.

**An answer never counts toward the two failed attempts** that bring up the dialog below. That
dialog is for the failure nobody can resolve, and an answer is the failure a waiter resolves in a tap.
Sending them off to write on paper because of a line they could have taken off the order would be the
wrong answer to a question that already has a right one.

**The phone asks one question about an attempt, and it is whether the laptop answered.** An answer,
whatever it said, means the laptop is there and has told the phone what exists, so an order no
attempt was ever left unanswered on stays open for changes, both ways of sending come back, and the
attempt counts for nothing. Silence means the order may already be at the laptop, so it freezes.
What the answer contained decides the wording on the screen and nothing else: a laptop that could not
save the order says exactly that, and its answer still leaves such an order open, because a laptop
that answers has not lost anything quietly.

**One silence outlasts every answer that follows it.** As soon as a single attempt for an order has
gone unanswered, that order is frozen for the rest of its life, whatever the laptop says to any
attempt after it. An answer of no describes the attempt the laptop has just seen, and it says nothing
at all about the attempt it never answered, which may have landed with the whole order on it. Were
the order to open again at that point, the waiter could take a line off, send once more, and get a
plain success back: the laptop would recognise the submission id, hand over the order it had stored
from the silent attempt, and the station would produce the order as it stood before the edit, with
the settled-or-open choice of that first attempt. That is the silent wrong answer this product must
never give, so the freeze that a silence starts ends in two ways only: the laptop accepts the order,
or the waiter says on the dialog below that the order is written down on paper. Both of those clear
the order and start an empty one. That an attempt was left unanswered is written into the record
beside the order in section 6.2, so a reload does not lift the freeze either.

**The waiter presses send, and the order freezes.** From that press until the phone knows what became
of it, the order takes no change of any kind, on any screen: no item added or removed, no note, no
table name, no delivery mode, and not the button that clears the lines which cannot be ordered. The
summary shows the freeze by greying those controls out. A whole screen cannot be greyed out, so a
waiter who reaches the items through the header link or the back gesture is put straight back on the
summary, which is where the failure and both ways out are. What the laptop pushes out in the
meantime still arrives and is still shown, because the item list is not the waiter's order and the
order is what freezes. The freeze is written to the browser's storage with the order, so a
reload does not lift it, and it ends in exactly three ways: the laptop accepts the order, the laptop
answers no and says why to an order no attempt was ever left unanswered on, or the waiter says on the
dialog below that the order is written down on paper. Acceptance and the dialog both clear the order
and start an empty one, and such an answer of no leaves it standing to be put right.

The lines and the total stay readable throughout, because the waiter may have to copy them onto
paper. The reason for the freeze is the submission id from section 6.3. A retry carries the id the
first attempt carried, so a laptop that already holds the order answers with the order it holds. Had
the waiter edited the order in between, the phone would show that answer as a plain success and the
edits would be gone with nobody told, which is the silent wrong answer this product must never give.

The retry takes the place of the two send buttons in the strip along the bottom of the summary, and
it reads "Erneut senden" in German and "Send again" in English. It stays tappable even when the
order holds an item that has sold out or left the menu, because a retry asks what became of an order
that may already exist rather than placing a new one.

**An attempt gives up after ten seconds.** A phone whose WiFi has gone waits about a minute for the
operating system to decide that nothing is coming, and for that whole minute the button reads "Wird
gesendet" and the waiter has nothing to do but hold the phone. Ten seconds is long enough for a busy
laptop on a good evening and short enough to stand at a table for. An attempt that runs out of time
is treated as any other failed attempt: it counts, it says the laptop could not be reached, and a
second one brings up the dialog below. The limit bounds the one attempt a person started, and it
never starts another.

**The page is loaded again while a send is on its way.** The request died with the page, so its
answer can never arrive. The phone reports that attempt as failed rather than as still running,
which is the only honest reading and the only one that does not leave the order frozen for the rest
of the evening. The message says the attempt was cut off while the order was on its way rather than
blaming the WiFi, the attempt is counted like any other, and the retry and the paper route are both
on the screen. The attempt is counted from the moment it starts, for exactly this reason: a tab that
dies mid-send must not buy the waiter a free try.

**Sending fails twice, and the phone stops offering the same thing again.** A dialog covers the
summary. It says in two short sentences that the laptop did not answer twice and that the order may
have arrived anyway, and it asks the waiter to write the order on a slip of paper and to look at the
tablet at the station. The tablet is a check a volunteer can carry out while walking, and it answers
the one question the phone cannot. Writing the order on paper and carrying it to the station is the
honest fallback when the laptop is not reachable, and it is what the crew were doing last year.

The dialog leaves two ways out and no others. "Bestellung ist aufgeschrieben", in English "The order
is written down", clears the order and starts an empty one so the waiter can serve the next table.
"Noch einmal versuchen", in English "Try again", sends once more for the case where the WiFi came
back while the waiter was reading, and the dialog returns if that attempt fails as well. There is no
close cross and a tap beside the dialog does nothing, because both honest answers are already on it.

**A frozen order the laptop refuses brings the same dialog up at once.** A refusal the waiter can act
on is one thing, and a refusal on an order that an earlier silence froze is another: the order takes
no change of any kind, so the item the laptop names cannot be taken off, and pressing the retry again
fetches the same answer back. There is nothing left on the phone for that waiter to try, which is
what the dialog is for, so it comes up on the first such refusal instead of waiting for a second
unanswered attempt. The order stays frozen behind it and every line stays readable for copying. The
dialog's first sentence still speaks of a laptop that was not reached, which is not what happened
here, and the red panel behind it names what the laptop actually said. A refusal on an order no
attempt was ever left unanswered on brings no dialog up at all, however often the laptop repeats it,
because there the waiter can put the order right and send it again.

**Reconnect.** The connection comes back, the app fetches whatever the current screen needs, and
replaces what it holds. The phone never has to work out what it missed.

**The device was signed out while it was offline**, because the admin set its owner up again, took
the waiter off the list, or started the laptop on a database that knows no devices at all. The next
call the phone makes is refused, and it does not matter which call that is: the check the app makes
when it starts, the send from the summary, or anything else the screen asks for. Every one of them is
read in the one place the answers pass through, so a waiter who was out of range and comes back to
press send is told that the phone has to be set up again rather than that the laptop could not be
reached. The app clears the token, **keeps the draft order**, and shows the setup screen saying the
started order is still there. **That refusal leaves no trace on the order.** The laptop turns the
call away at the door, before a line of ordering code has read anything, so nothing was stored and
there is nothing for the phone to report: it writes down no reason, it does not freeze the order, and
it takes back the attempt it had begun to count. The summary the waiter comes back to after scanning
a fresh code is the one they left, table name, lines and all. If an earlier attempt on that order had
gone unanswered, the order is still frozen and the sentence that stood on it is still standing, which
is the truth, because being set up again says nothing about whether that earlier attempt arrived.
Throwing away a
half-built order because an admin tapped the wrong row would be destroying a guest's order to solve
an administrative problem. The order keeps the submission id it already had, so once a fresh code has
been scanned and the waiter sends again, an attempt that did reach the laptop earlier comes back as
the order the laptop already holds instead of becoming a second one. An answer that refuses only what
a device may do, such as a station's tablet asking for a waiter's screen, is a different answer and
signs nobody out.

**The device is signed out and set up again.** The person keeps their identity and everything
attached to it, because the code the admin issued names them. The orders they already placed are
unaffected.

**An item sold out, a price changed, or an item moved to another station while the basket was
open.** The line stays, it is flagged, and the phone shows the price the laptop now names, which is
also the price that travels back with the order. A flagged line holds the send back until the waiter
takes it off with the one button that clears such lines (section 3.4).

**Two phones send the same order.** Not prevented, and not preventable: two waiters can genuinely
take the same table. The station sees two orders with two different numbers, which is the same
situation as two paper slips, and is resolved the same way. The submission id protects against one
order being sent twice, not against two people taking the same order, which is a problem software
cannot see.

### 6.5 The controls that end something stay where the thumb expects them

The way on and the way back sit in a strip along the bottom of the screen that stays put while the
list above it scrolls. On the categories that strip carries the running total and the way to the
summary, inside a category it carries the one button back to the categories, and on the summary it
carries the total and both ways of sending, or the retry once a send has failed. A waiter never
scrolls to find the way out or the way on.

**A strip like that ignores a tap that arrives on the heels of a scroll**, meaning within a few
tenths of a second of the list coming to rest, and it ignores any tap whose finger moved before it
lifted. Sending cannot be undone, because the part of the order is at the station the moment it
lands, and a finger coming down to stop a flying list must never send an order by accident. A waiter
who scrolls, stops, looks and then presses notices nothing.

**Both ways of sending ask once before anything leaves the phone.** A tap on either send
button puts a short question on the screen. Under it stands one labelled line per fact: the
table, the amount, and then one line for each station saying what that station will do with
its part, carrying the waiting time when that part goes out together. The button that
confirms carries the same words the waiter just tapped, so whether the order is settled now
or left open is read off that button and the question needs no sentence about it. Only that
second tap sends anything. Backing
out closes the question and changes nothing: the order stands as it was and the
settled-or-open choice is forgotten, so a waiter who taps the wrong one of the two starts the
choice afresh. The retry after a failed send is not asked twice, because a waiter who reaches
for it has already decided to find out what became of an order the laptop may be holding.

---

## 7. The program on the laptop and its setup

### 7.1 The desktop application

The program the volunteer starts is an **Avalonia desktop application**, not a console window. The
window is what the operator sees all evening, and it exists to make three failures impossible or
visible: the program being closed by accident, the laptop going to sleep, and nobody noticing that no
device can reach the laptop.

**One process serves the devices.** The desktop application does not launch a service or a second
long-lived executable. Two servers would mean two things to close, two things to crash, and a
volunteer who can see one of them running while the other is gone.

**The one exception is the setup step.** Elevation on Windows is always a new process: a program
running as a standard user cannot raise its own rights, so the firewall rule and the folder
permission grant in section 7.3 cannot be performed in process. What the program launches for them is
itself, elevated, with a setup argument. That process does those two things and exits. It hosts no
web application, opens no port and lives for a second or two.

#### The boundary between the window and the admin pages

**The admin interface is the web page, and the desktop window never becomes a second one.** The web
admin has to exist regardless, because the devices are browsers and the person setting them up is
already in a browser. A second administrative surface in the window would have to be kept true
against the first, and the two would disagree on the evening one of them was not updated.

The window is a launcher and a status light. What lives in it:

1. **Nothing at all while the server is healthy.** No line saying that the program is running,
   because the window being on the screen is the proof. A window that always says everything is fine
   trains the operator to stop reading it.
2. **Errors in plain language, on the window.** The port is already taken, the data folder cannot be
   written to, no network was found, the server stopped. These are shown where the person is looking,
   not written to a log nobody opens.
3. **Buttons: open the admin pages, open the data folder, repair the setup, quit.** The button that
   opens the admin pages is the largest and is never hidden behind a menu.
4. **The language picker**, because it is one of the few settings that cannot live in a web page
   served by the very server being configured.

#### Closing the window may never end the evening

**Clicking the window's close button minimises the window. It never stops the server.** Quitting
happens only through the quit button, and the quit button asks for confirmation first.

This is the entire justification for the desktop application existing. In a console design, the one
gesture every computer user makes without thinking, clicking the cross in the corner, would end
ordering for the whole festival, and the only defence would be a bold line in a checklist that the
person who closed the window had not read.

**The program does not open a browser by itself when it starts.** A volunteer restarting the program
at 20:30 because something looked wrong does not want a browser window arriving on top of what they
were doing.

#### Only one instance may run

A second launch must not produce a second server. The application takes a named lock when the window
starts; when the lock is already held, the second instance tells the first, the first brings its
window to the front, and the second exits without showing anything. The port bind is the backstop for
the case the lock cannot catch, such as a second Windows user signed in through fast user switching,
and that surfaces as the plain "the port is already taken" message rather than a crash.

#### The port and the bind address

**The port is chosen by Windows and never typed.** On first start the program asks for a free port
and writes it down. Every later start tries that port; when it is taken the program asks for another
one, writes that down, and tells the operator in the window that everybody has to set their device up
again. That is not a courtesy: a device's token is stored against the full address including the
port, so a new port means the token is gone rather than merely stale.

**The server always answers on every network interface.** It is the only value that keeps working
when the router hands the laptop a different address tomorrow. The admin pages are restricted
separately, by refusing admin requests that do not come from the machine itself (section 2.5), so
binding wide costs nothing.

#### A folder it cannot write to stops the program rather than every order

On startup the backend checks that the data folder is writable. When it is not, the window shows the
repair text naming the folder and the program does not start serving. A program that starts and then
silently fails every order is the worst possible response to a folder it cannot write to.

#### Logging

A rolling daily log file is written into the data folder and kept for fourteen days. The window and
the server it hosts write to the same file. Recorded: the port asked for and granted, the data
folder, startup and shutdown, every bind failure, and the whole device setup lifecycle, because when
devices fail to set up at a festival the log is the only account of what happened. **Device tokens
and setup codes never appear in it.** Ids, addresses and outcomes are what a phone call about a
failed setup is actually about, and those are what is written.

### 7.2 Where the data lives

**The database, the log and the backup files live in `C:\ProgramData\GastronomyApp\`.** They do not
live beside the executable.

The reason is the realistic case at a fire department: several people take turns operating the laptop
and they do not all sign in as the same Windows user. `ProgramData` is reachable no matter who signs
in. A folder under a user's own profile is not, and a folder beside the executable depends on where
somebody happened to drop it.

**The permissions trap, which breaks exactly the case this choice exists to serve.** A new folder
created under `ProgramData` inherits an access control list that gives its creator full control and
everyone else read access only. So the volunteer who sets the system up at home can write to it, and
the different volunteer who signs in at the festival can read the database and cannot write to it.
Orders then start failing for a reason nobody present could guess, on the evening it matters.

**When the application creates its folder it therefore grants the `Users` group modify rights on it,
explicitly**, so that files and subfolders created later carry the same rights. This needs no
administrator rights, because the owner of a folder may always change that folder's own access list,
but it does have to be done deliberately.

**That grant needs no administrator rights only for the folder's owner, which is why the repair is
not tied to first run.** The check is not "have we run before" but **"is the required state
present"**, evaluated on every start:

* If the data folder does not exist, the program creates it and grants the `Users` group modify
  rights, which needs nothing elevated because it is creating the folder it then owns.
* If the folder exists and the current user can write to it, nothing happens and nothing is shown.
* **If the folder exists and the current user cannot write to it, the program offers the elevated
  repair in section 7.3 and does not offer a different folder.** Any Windows user who can answer the
  elevation prompt can run it, which is the point: the person locked out is by definition not the
  owner.

**What this means for updating the program.** The executable and the data are in different places, so
replacing the executable with a newer one does not touch the database, the log or the backups. An
update is a file copy, and the evening's history survives it.

**On platforms other than Windows** the data folder is the platform's own per-user application data
location and the access control grant does not happen. Version 1 targets Windows.

### 7.3 First run, and the one elevation

**The program does not run as an administrator.** Three reasons are worth keeping written down: an
elevation prompt on every start is a scary dialog put in front of exactly the person who cannot judge
it; a laptop whose operator is a standard user could not run the program at all; and a web server
bound to every interface on an open WiFi is a much larger liability with full machine rights behind
it.

Instead, **the work that needs elevation happens in one short elevated step**, and the program runs
as a normal user for the rest of its life. Two things happen in that step:

1. **The inbound firewall rule for the program is created.**
2. **The data folder is created and made writable by anyone who may sign in** (section 7.2).

**Both actions are idempotent, and the step is offered whenever its result is missing rather than
once per installation.** Creating a firewall rule that already exists replaces it with the same rule.
Granting `Users` modify rights on a folder that already has them changes nothing. So the program
checks the required state on every start and offers the step whenever something is missing, which is
what makes it a repair as well as a first run.

**The two actions are attempted independently, and a failure in one is never told as a decline.** A
laptop whose policy forbids firewall changes fails the first action, and the second one still has to
happen, because a data folder nobody can write to fails every order. The elevated process attempts
both, writes what went wrong for each into the log file, and reports back that elevation was granted
and a step still failed. Telling somebody they declined a prompt they accepted sends them to fix the
wrong thing.

#### Why the firewall rule is created deliberately and never left to the prompt

The Windows Defender Firewall prompt that appears when a program first listens on a port looks like
it solves this, and it does not.

* **The prompt cannot be re-triggered.** Once a decision has been recorded for a program, Windows
  does not ask again. A volunteer who clicked "Cancel" while carrying a crate of glasses has silently
  decided the question for every future evening.
* **Worse, Microsoft documents that when the user lacks administrative rights, block rules are
  created no matter which button is clicked.** The prompt in that case is not a question. It is a
  block rule with a dialog in front of it, and the devices cannot reach the laptop afterwards.

So the rule is created with an elevated command during first run. It is scoped to the program rather
than to a bare port, restricted to the private profile and to the local subnet, and it allows the
inbound connection the devices need.

#### The repair button afterwards

Somebody will decline the elevation, and somebody will arrive at a laptop where the rule was never
created or where the data folder belongs to a colleague. The window therefore keeps **"Einrichtung
reparieren"** available for the rest of the program's life. It runs the same elevated step with the
same two idempotent actions.

**It is one button rather than two because a volunteer cannot tell the two failures apart.** Devices
that cannot reach the laptop and orders that cannot be written both look like "the program is
broken", and asking somebody at 20:00 to work out which of two repairs they need is asking them to
diagnose. If nobody present can answer the elevation prompt, the fallback is instructions and nothing
else: the program opens the Windows firewall settings and does not pretend the repair happened.

### 7.4 What the admin configures, in order

The order matters, because each step needs the one before it. The overview screen enforces it by
naming the next missing thing rather than letting the admin wander.

1. **The festival.** Its name, its start and its end. Everything below belongs to it, so nothing else
   can be done first. A festival like last year's is copied instead of typed again.
2. **Stations.** One per kitchen or bar, and each one is added to this festival. At a normal site
   this is two rows.
3. **Categories.** The headings the items are sorted under, each with a colour, because on the phone
   a category is a large coloured button. Categories are shared by every festival.
4. **The menu.** Put each item on this festival's menu, with the price it costs here, the stations of
   this festival that prepare it, and a preparation time for anything that takes a while. An item
   needs at least one station, and every item belongs to exactly one category, which is why the
   categories come first.
5. **Set up the station tablets.** One per station, from the stations page.
6. **Set up the phones.** Last, because a phone fetches the catalog when it is set up. One waiter at
   a time: put the person on the list, create their QR code, and they scan it.

### 7.5 Setup checklist, English

Print this page and take it with you.

**At home, the day before**

1. Copy the program onto the laptop and start it. The first time, Windows asks once whether the
   program may make a change: confirm it. The program uses that one moment to allow access from the
   network and to create its data folder, and it never asks again.
2. In the program window, click "Open the admin pages".
3. **Create the festival.** Give it a name, a start and an end, and set the period generously: if the
   evening runs from five until three in the morning, enter noon until three the following afternoon.
   Everything below belongs to this festival, so it comes first. If you ran this festival before, open
   the old one and click "Copy" instead. The stations, the items and the prices come across, and you
   only fill in the name and the dates and correct the prices that changed.
4. Open the festival and add the stations to it, for example Kitchen and Bar. A station you used last
   year is already in the list and only has to be added; a new one is created here.
5. Create the categories the items are sorted under, for example Food and Drinks, and pick a colour for
   each one. On the phone a category is a large coloured button, and tapping it opens the items of
   that category with one button back to the list. Every item belongs to exactly one category, so
   this step comes before the items. Categories are shared by every festival, so renaming one here
   renames it everywhere. The "New category" button is at the bottom of the items page, beside
   "New item".
6. Put the items on this festival's menu. For each one, enter the price it costs here and tick which
   of this festival's stations prepare it. Food usually gets only the kitchen. Beer at a place with
   two bars gets both, and the waiter then picks one while taking the order. Where an item takes a
   while to make, fill in the preparation time in minutes as well, so the waiters can tell a guest
   roughly how long they will wait. Leave that field empty for drinks and anything else that is handed
   over right away.
7. Take one phone, set it up, and place a couple of practice orders so you have seen the screens once
   before the evening. Delete nothing afterwards. If you would rather the evening started at order
   number 1, practise on a festival you created for practising and create the real one afterwards.

**On site, before the guests arrive**

8. Switch on the WiFi router and connect the laptop to the same network the phones and the tablets
   will use.
9. **Switch off client isolation in the WiFi router.** It is sometimes called AP isolation or guest
   mode. With it switched on the phones cannot reach the laptop, and nothing else in this list will
   help.
10. **Give the laptop a fixed address.** Either reserve one for it in the router, which is usually
    called a DHCP reservation, or set a static address on the laptop's WiFi adapter. If the address
    changes during the evening, every phone and every tablet loses the laptop at once and every one of
    them has to be set up again, one at a time.
11. Plug the laptop into power and leave the lid open. The program keeps the laptop awake by itself,
    so there is nothing to change in the power settings. A closed lid still sends it to sleep.
12. Take one phone and scan the QR code shown on the admin pages. Seeing any page from the program at
    all is the proof that the devices reach the laptop. If nothing opens, go back to step 9, and then
    use "Repair the setup" in the program window.
13. Check that the festival is running. Open the festivals page: the one for tonight has to say
    "Running now". If it does not, the start or the end is wrong, and "Edit" on that row puts it
    right.
14. **Set up one tablet per station.** Open the stations page, tap "Set up the tablet" on the first
    station, and scan the QR code with the camera of the tablet that will stand there. The tablet then
    shows that station's orders and nothing else. Carry it to the station, plug it in, and leave it
    switched on: the screen has to stay awake all evening.
15. Set the phones up one at a time. Open the waiter list and tap "New waiter", which shows a QR
    code. Let that waiter scan it with the camera of their own phone and type their name there. The
    name they type is what puts them on the list, and then you move on to the next person. If
    somebody already on the list needs a different phone, tap "Set the phone up again" on their row
    instead and let them scan that code, which asks them nothing.
16. Place one order from a phone and watch it appear on the right station's tablet. That is the whole
    system proven in ten seconds, and it is worth doing before the first guest sits down.

**During the festival**

17. Leave the program running. Clicking the cross in the corner only puts the window away: the program
    carries on taking orders, and only "Quit the program" stops it.
18. Show whoever is working a station the three things their tablet does: start preparing, mark as
    ready, and the same two for a whole order at once. When something is marked ready the tablet shows
    the table, and that is what gets written on the tray.
19. **Nobody is called when food is ready.** The tray stands at the hatch with the table written on
    it, and whichever waiter passes next takes it. Tell the waiters that once at the start of the
    evening, because it is the one thing about the system that is not obvious from a screen.
20. When a station tells you something has run out, walk to the laptop and tap that item to "Sold out"
    in the item list. The laptop is the only place that switch exists, and one tap puts the item back
    when another crate turns up.
21. **If a tablet dies, set up a replacement.** Open the stations page, tap "Set up the tablet" on that
    station, and scan the code with another tablet or with any spare phone. The station's orders are
    all still there: nothing was stored on the device.

**Afterwards**

22. Look at the station tablets one last time and make sure every list is empty. That is the one check
    that catches an order nobody produced, and it takes five seconds.
23. Click "Open the data folder" in the program window and copy the whole folder onto a USB stick,
    with the program still running. It holds the evening's orders. The backup button described in
    section 7.8 is not built yet, which is why the whole folder is copied rather than one file: the
    most recent orders can still be sitting beside the database rather than in it.

### 7.6 Setup checklist, German

Drucken Sie diese Seite aus und nehmen Sie sie mit.

**Zu Hause, am Tag vorher**

1. Kopieren Sie das Programm auf den Laptop und starten Sie es. Beim ersten Start fragt Windows einmal
   nach, ob das Programm eine Änderung vornehmen darf: bestätigen Sie das. Das Programm gibt in diesem
   einen Moment den Zugriff aus dem Netzwerk frei und legt seinen Datenordner an, und danach fragt es
   nie wieder.
2. Klicken Sie im Programmfenster auf "Verwaltung öffnen".
3. **Legen Sie das Fest an.** Geben Sie ihm einen Namen, einen Beginn und ein Ende, und fassen Sie den
   Zeitraum großzügig: Wenn der Abend um 17 Uhr anfängt und um 3 Uhr endet, tragen Sie 12 Uhr bis 15
   Uhr am nächsten Tag ein. Alles Weitere gehört zu diesem Fest, deshalb steht es am Anfang. Gab es
   dieses Fest schon einmal, öffnen Sie das alte und klicken Sie auf "Kopieren". Die Ausgabestellen,
   die Artikel und die Preise kommen mit, und Sie tragen nur den Namen und die Daten ein und ändern
   die Preise, die sich geändert haben.
4. Öffnen Sie das Fest und fügen Sie ihm die Ausgabestellen hinzu, zum Beispiel Küche und Theke. Eine
   Ausgabestelle vom letzten Jahr steht schon in der Liste und muss nur hinzugefügt werden; eine neue
   legen Sie hier an.
5. Legen Sie die Kategorien an, unter denen die Artikel einsortiert werden, zum Beispiel Speisen und
   Getränke, und wählen Sie für jede eine Farbe. Auf dem Telefon ist eine Kategorie ein großer farbiger
   Knopf, und ein Tippen darauf öffnet die Artikel dieser Kategorie, mit einem Knopf zurück zur Liste.
   Jeder Artikel gehört zu genau einer Kategorie, deshalb kommt dieser Schritt vor den Artikeln. Die
   Kategorien gelten für alle Feste: Wenn Sie hier eine umbenennen, heißt sie auf jedem Fest so. Der
   Knopf "Neue Kategorie" steht unten auf der Artikel-Seite, neben "Neuer Artikel".
6. Setzen Sie die Artikel auf die Karte dieses Festes. Tragen Sie bei jedem den Preis ein, den er hier
   kostet, und kreuzen Sie an, welche Ausgabestellen dieses Festes ihn zubereiten. Essen bekommt meist
   nur die Küche. Bier bekommt an einem Platz mit zwei Theken beide, und der Kellner wählt dann beim
   Aufnehmen aus. Wo ein Artikel eine Weile braucht, tragen Sie auch die Zubereitungszeit in Minuten
   ein, damit die Kellner einem Gast ungefähr sagen können, wie lange er wartet. Bei Getränken und
   allem anderen, was sofort über die Theke geht, lassen Sie das Feld leer.
7. Richten Sie ein Telefon ein und geben Sie ein paar Übungsbestellungen auf, damit Sie die Bildschirme
   einmal gesehen haben. Löschen Sie danach nichts. Wenn der Abend bei Bestellung 1 anfangen soll,
   üben Sie auf einem Fest, das Sie zum Üben angelegt haben, und legen Sie das richtige danach an.

**Am Festplatz, bevor die Gäste kommen**

8. Schalten Sie den WLAN-Router ein und verbinden Sie den Laptop mit demselben Netz, das die Telefone
   und die Tablets nutzen.
9. **Schalten Sie im WLAN-Router die Client-Isolierung aus.** Sie heißt manchmal AP-Isolation oder
   Gastmodus. Solange sie eingeschaltet ist, erreichen die Telefone den Laptop nicht, und nichts
   anderes aus dieser Liste hilft dagegen.
10. **Geben Sie dem Laptop eine feste Adresse.** Reservieren Sie ihm eine im Router, das heißt dort
    meist DHCP-Reservierung, oder stellen Sie am WLAN-Adapter des Laptops eine feste Adresse ein. Wenn
    sich die Adresse während des Abends ändert, verlieren alle Telefone und alle Tablets auf einen
    Schlag die Verbindung, und jedes einzelne muss neu eingerichtet werden.
11. Schließen Sie den Laptop ans Stromnetz an und lassen Sie ihn aufgeklappt. Das Programm hält den
    Laptop von selbst wach, an den Energieeinstellungen müssen Sie nichts ändern. Zugeklappt geht er
    trotzdem in den Ruhezustand.
12. Nehmen Sie ein Telefon und scannen Sie den QR-Code, der in der Verwaltung steht. Schon dass
    überhaupt eine Seite des Programms erscheint, ist der Beweis, dass die Geräte den Laptop
    erreichen. Wenn sich nichts öffnet, gehen Sie zurück zu Schritt 9 und nehmen Sie danach im
    Programmfenster "Einrichtung reparieren".
13. Prüfen Sie, ob das Fest läuft. Öffnen Sie die Seite Feste: Bei dem Fest von heute muss "Läuft
    gerade" stehen. Steht es dort nicht, stimmt der Beginn oder das Ende nicht, und "Bearbeiten" in
    dieser Zeile bringt es in Ordnung.
14. **Richten Sie an jeder Ausgabestelle ein Tablet ein.** Öffnen Sie die Seite Ausgabestellen, tippen
    Sie bei der ersten Ausgabestelle auf "Tablet einrichten" und scannen Sie den QR-Code mit der Kamera
    des Tablets, das dort stehen soll. Danach zeigt das Tablet die Bestellungen genau dieser
    Ausgabestelle und sonst nichts. Tragen Sie es an die Ausgabestelle, schließen Sie es ans Stromnetz
    an und lassen Sie es eingeschaltet: der Bildschirm muss den ganzen Abend wach bleiben.
15. Richten Sie die Telefone nacheinander ein. Öffnen Sie die Liste der Kellner und tippen Sie auf
    "Neuer Kellner", worauf ein QR-Code erscheint. Lassen Sie den Kellner ihn mit der Kamera seines
    eigenen Telefons scannen und dort seinen Namen eingeben. Dieser eingegebene Name ist es, der ihn
    in die Liste aufnimmt, und danach machen Sie mit der nächsten Person weiter. Braucht jemand, der
    schon in der Liste steht, ein anderes Telefon, tippen Sie stattdessen in seiner Zeile auf
    "Telefon neu einrichten" und lassen Sie ihn diesen Code scannen, der nichts abfragt.
16. Geben Sie eine Bestellung von einem Telefon auf und sehen Sie zu, wie sie auf dem Tablet der
    richtigen Ausgabestelle erscheint. Damit ist das ganze System in zehn Sekunden geprüft, und das
    lohnt sich, bevor der erste Gast sitzt.

**Während des Festes**

17. Lassen Sie das Programm laufen. Ein Klick auf das Kreuz in der Ecke legt nur das Fenster weg: das
    Programm nimmt weiter Bestellungen an, und nur "Programm beenden" hält es an.
18. Zeigen Sie den Leuten an einer Ausgabestelle die drei Dinge, die ihr Tablet kann: Zubereitung
    beginnen, fertig melden, und dasselbe für eine ganze Bestellung auf einmal. Wenn etwas fertig
    gemeldet wird, zeigt das Tablet den Tisch, und genau der wird auf das Tablett geschrieben.
19. **Es wird niemand gerufen, wenn etwas fertig ist.** Das Tablett steht mit dem Tisch darauf an der
    Ausgabe, und der nächste Kellner, der vorbeikommt, nimmt es mit. Sagen Sie das den Kellnern einmal
    am Anfang des Abends, denn es ist das Einzige am System, das man einem Bildschirm nicht ansieht.
20. Wenn eine Ausgabestelle meldet, dass etwas alle ist, gehen Sie zum Laptop und tippen Sie den
    Artikel in der Artikelliste auf "Ausverkauft". Nur am Laptop gibt es diesen Schalter, und ein
    Tippen stellt den Artikel wieder zurück, wenn noch eine Kiste auftaucht.
21. **Wenn ein Tablet ausfällt, richten Sie ein Ersatzgerät ein.** Öffnen Sie die Seite
    Ausgabestellen, tippen Sie bei dieser Ausgabestelle auf "Tablet einrichten" und scannen Sie den
    Code mit einem anderen Tablet oder mit irgendeinem freien Telefon. Die Bestellungen der
    Ausgabestelle sind alle noch da: auf dem Gerät war nichts gespeichert.

**Danach**

22. Sehen Sie zum Schluss noch einmal auf die Tablets und prüfen Sie, dass jede Liste leer ist. Das ist
    die eine Kontrolle, die eine Bestellung findet, die niemand zubereitet hat, und sie dauert fünf
    Sekunden.
23. Klicken Sie im Programmfenster auf "Datenordner öffnen" und kopieren Sie den ganzen Ordner auf
    einen USB-Stick, während das Programm noch läuft. Darin stehen die Bestellungen des Abends. Die
    Schaltfläche für die Sicherungsdatei aus Abschnitt 7.8 ist noch nicht gebaut. Deshalb wird der
    ganze Ordner kopiert und nicht eine einzelne Datei: die letzten Bestellungen können noch neben der
    Datenbank liegen statt in ihr.

### 7.7 Configuration that is not in the UI

**`appsettings.json` is no longer a file any human opens.** It ships beside the executable, it holds
the shipped defaults for the scheme, the port, the bind address and the log level, and a volunteer
never sees it. Asking somebody with little technical ability to edit JSON on the evening the port is
taken was never a workable answer.

What a volunteer changes is written to `settings.json` in `C:\ProgramData\GastronomyApp\`, which
overrides the shipped defaults. That location is fixed and is not itself configurable, because the
data folder path is one of the settings stored in it and a setting cannot say where it is kept.

### 7.8 The backup, and why it is a button

**This is designed and not built yet.** There is no backup endpoint, no backup screen and no
diagnostics screen in the code today. The reasoning below is kept because it is the design somebody
will build from, and section 8.2 lists it as outstanding.

The database runs in a journal mode that is right for one writer and several readers. It also means
that the most recent transactions live in a file beside the database rather than in the database
file, so copying the one file a volunteer can see loses the end of the evening, silently, which is
the worst possible way to lose data.

The backup screen therefore has a button. It writes one consistent file, dated, into the data folder,
with everything in it, and then names that file on screen so the volunteer knows exactly which one to
drag onto the USB stick.

**Finding that file is the program window's job, not the admin page's.** A web page cannot open a
folder on the machine it is served from. So the admin screen names the file and the path, and the
window's "Datenordner öffnen" button is what actually opens it.

### 7.9 What is verified by hand

The Windows integrations of the desktop application sit behind tested interfaces, but the operating
system half of each one needs a real machine, and the evening the product exists for is a walk from a
phone to a station and back that no test suite watches the way a person does.
`docs/manual-verification.md` is that list, and it is worked through once on a real machine before a
demonstration.

---

## 8. What is deliberately not built, and what is still open

### 8.1 Decisions that closed a question

**No printers, ever.** The fire department chose tablets before any printer hardware was bought.
Printing is not deferred and not a version 2 item: it is gone, and nothing about slips, print state,
paper or printer hardware exists anywhere in the product.

**Nobody is notified when food is ready.** Section 4.5 gives the reasoning. A notification that
cannot be relied on in a loud marquee is worse than none, because it teaches everybody to stop
walking past the station.

**Delivery mode cannot be changed after sending.** Section 4.1. The station has already arranged its
work around the answer by the time anybody would want to change it.

**Ready is final.** Section 4.2. A state that can be taken back would be the software telling a human
something untrue about a tray that has already left.

**Admin access is from the laptop and nowhere else.** Section 2.5. On an open WiFi with plain HTTP,
any admin credential reachable from the network is readable off the air. That means marking an item
sold out is a walk to the laptop, and the owner has accepted that.

**German uses the Sie form throughout.** A fire department crew says du to each other, but the text
is read by whoever is holding the device, including people helping out for one evening, and Sie is
the form that is never wrong for any of them.

**A name the devices could use instead of an address.** Deferred, and the findings that decided it
are recorded here so nobody has to rediscover them. A WiFi repeater in NAT mode passes IP traffic and
never passes multicast, so a `.local` name would fail for exactly the waiters standing furthest from
the access point. Android 11 and older cannot resolve `.local` at all. A WiFi network with no
internet can leave cellular as the phone's default network, and cellular is documented as excluded
from `.local` resolution. Chrome may treat a typed `.local` name as a search term, which on a network
with no internet produces an offline error page that a volunteer reads as "the system is down".
Windows has no built-in way to advertise a hostname. The QR code degrades gracefully across all of
that, and a name does not.

One finding that is not actionable now but is worth watching: **Android 17 will gate all local
network traffic behind a permission**, with no documented browser exemption yet. That would affect
the QR code path too, so it is a risk to the product as it stands rather than an argument about a
future version.

### 8.2 Specified once, and not built

These were designed in an earlier version of this document and do not exist in the code. They are
recorded so that nobody rebuilds one by accident, and so that the reasons are findable if one is
asked for again.

**A six digit setup code as a fallback for a broken camera.** The invitation carries a hashed QR code
and nothing else, so there is no second secret to verify a typed code against. Some German and
English strings for such a screen are still in the locale files and are not reachable from anything.
If the fallback is wanted, it is a second hashed secret on the invitation, a cap on wrong attempts,
and a screen, and it should be specified before it is built rather than pieced together from the
leftover strings.

**A practice mode.** An earlier design had a practice mode whose orders were kept out of the
treasurer's figures. It does not exist. The festival now carries the numbering (section 5.2), so an
evening starts at one by itself, and anybody who wants their practice orders somewhere else creates a
festival to practise on and the real one afterwards.

**A curated list of table names in the admin.** The suggestions come from the table names already
typed on orders (section 3.3). A few strings for an admin screen are left in the locale files and are
not reachable from anything.

**The backup button and the diagnostics screen.** Section 7.8 explains why copying the database file
by hand is not good enough and why the backup belongs behind a button. Neither the button nor the
endpoint behind it exists, and neither does the diagnostics screen that would show the version, the
paths and the log file. The German and English wording for both is written and sits unused in the
locale files. Until they are built, what a volunteer can actually do is copy the whole data folder
with the program running, which the setup checklist says.

**A CSV export of the evening for the treasurer.** An earlier version of this document specified a
semicolon separated, byte-order-marked file that German Excel opens correctly. Nothing answers for it
today. The format is worth keeping if it is asked for again, because the two details that make it
work, the semicolon and the byte order mark, are exactly the two a first attempt gets wrong.

**A screen that names a waiter's takings.** Which waiter collected the money is recorded on every
settled item (section 3.7), and nothing reads it back out yet.

### 8.3 Genuinely open

1. **How long a station tablet's list should stay on screen after everything is ready.** Today a card
   leaves as soon as its last item is ready, which is right while it is busy and may be too abrupt at
   the end of the evening, when somebody wants to look back at what just went out. Nothing is lost
   when it disappears, because the row is in the database, but there is no screen at the station that
   shows it. Worth watching on one real evening before anything is built.

2. **Whether one tablet per station is enough at a busy kitchen.** The model allows exactly one, and
   the reasoning is that two screens showing the same list would let two cooks start the same item. A
   real evening will say whether a large kitchen wants a second read-only screen, which is a different
   thing from a second tablet and should be specified as one if it is wanted.

3. **Whether the production times the admin types turn out to be worth typing.** The estimate is only
   as good as the numbers behind it, and nobody has yet filled a real menu in. The status change log
   in section 3.8 is what will answer this after the first festival: it holds how long each step
   actually took, so the guesses can be compared against the evening rather than argued about.
