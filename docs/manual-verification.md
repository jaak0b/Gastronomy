# Manual verification on a real Windows machine

Two things can only be proven by hand. The desktop application's Windows integrations sit behind
tested port interfaces, but the operating system half of each one needs a real machine. And the
evening the product exists for is a walk from a phone to a station and back, which no test suite
watches the way a person does.

Work through this list once on a real machine before the first demonstration, with at least one phone
and one tablet on the same WiFi as the laptop. Every item names what success looks like.

## First run and setup

1. **Clean first run.** Launch the executable on a machine that has never run it. Exactly one UAC
   prompt appears. Afterwards `%ProgramData%\GastronomyApp` exists and its Properties > Security shows
   `Users` with Modify rights, and Windows Defender Firewall > Inbound Rules contains a rule named for
   the program: TCP, private profile, local subnet.
2. **Setup run twice.** Run the elevated setup again on a machine that already has the rule. The rule
   is updated in place and is never absent between commands.
3. **Decline the UAC prompt.** The window still opens, shows the declined-setup text, and the server
   still starts.
4. **Decline by closing the dialog.** Close the one-time setup dialog with its X instead of the
   button. The declined text appears on the main window, not only in the dialog, and the server still
   starts.

## Single instance and lifecycle

5. **Second launch while the first runs.** No second window appears, the first window comes to the
   front and takes focus, and the second process exits within about two seconds.
6. **Second launch during startup.** Launch a second instance immediately after the first, before its
   window has painted. The second still exits quietly, with no crash dialog, even if it could not
   signal the first.
7. **Close button.** Clicking the X minimises. A phone on the same WiFi keeps getting answers while
   the window is minimised.
8. **Tray icon.** Right-clicking shows exactly two items, open the admin pages and quit, and both
   work. Left-clicking the icon restores the window.
9. **Quit.** The quit button shows the confirmation. Keeping it running changes nothing. Confirming
   stops the server (a phone request now fails) and exits the process. Afterwards `powercfg /requests`
   shows no remaining SYSTEM request for the executable.

## Power and network

10. **Sleep suppression.** With the server running, `powercfg /requests` lists a SYSTEM request for
    the executable. After quitting, the request is gone and the laptop sleeps normally.
11. **No network.** Disable every network adapter and start. The window shows the no-network error.
12. **The address the QR code carries.** Open the admin pages and put one waiter on the list, because
    every QR code is created for a named waiter or a named station and there is no code without one.
    Create that person's code and scan it with a phone on the same WiFi. The site opens. On a laptop
    that also has virtual adapters, do this twice with a restart in between: the address in the code
    is the same both times and is the one the phone can actually reach, not a `169.254.x.x` address.

## Failure states

13. **Port in use.** Occupy the configured port with another program, then start. The window names
    the port in the error and does not crash.
14. **Unwritable data folder.** Revoke your own Modify rights on the data folder. The window shows the
    repair text, and the repair button on the window restores write access after one UAC prompt.
    Declining that prompt shows the declined text and opens the Windows firewall settings page.
15. **Wrong folder named.** Point the data folder setting at a path the current user cannot write to
    and restart. The repair text names that path, not `%ProgramData%\GastronomyApp`.
16. **Generic start failure.** Make the server fail to start for a reason that is neither the port
    nor folder permissions (a corrupt `gastronomy.db` is the easiest to stage). The window shows the
    could-not-be-started text, names no folder path, and the process stays alive with no crash
    dialog.

## An evening, end to end

This is the walk the product exists for. Do it with a real phone in one hand and a real tablet
standing where a station would stand, because reading it off two browser tabs on the laptop hides
exactly the problems this section is looking for.

17. **Set the menu up.** In the admin pages open Feste and create a festival whose period covers
    today generously, for example from this morning until tomorrow afternoon. Its row says "Läuft
    gerade". Open it, and every page below is about that festival. Add two stations to it, for example
    Küche and Theke. The overview then asks for a category, because an item cannot be created without
    one. On the Artikel page tap "Neue Kategorie" twice, for example Speisen and Getränke, and pick a
    colour for each one. The heading of each category is written on that colour, and the lettering
    stays readable on a light colour as well as on a dark one. Move Getränke above Speisen with the
    arrows beside its heading, and the two headings swap places. Neither category is on the phones
    yet, because a category with no items in it, or with all of its items switched off, does not
    appear on a phone at all. Create one item in Speisen that takes a while and put it on this
    festival's menu with a price, a preparation time in minutes, and the kitchen as its only station.
    Create one drink in Getränke, put it on the menu with a price and the bar as its only station, and
    leave the preparation time empty. The overview now asks for one thing only, and it asks it once
    per station: set that station's tablet up.
18. **Enrol a station tablet.** On the stations page, tap "Tablet einrichten" on the kitchen and scan
    the QR code with the tablet's own camera. The tablet lands straight on the kitchen's own page,
    with the kitchen's name on it and no other station's work. It asks for no name. On the laptop, the
    station's row now shows that a tablet is set up, and the kitchen's line has gone from the overview.
19. **Enrol a second tablet, and a phone.** Do the same for the bar with a second tablet. Then open
    the waiter list and tap "Neuer Kellner", which shows a code that belongs to nobody yet. Scan it
    with a phone, and the phone asks for a name. Type one and send it. The waiter now appears in the
    list under that name, the phone is ready, and the overview says that everything is set up.
    Tapping "Telefon neu einrichten" on their row afterwards produces a code that names them, and
    a phone scanning that one is never asked for a name.
20. **Replace a tablet.** Tap "Tablet einrichten" on the kitchen again and scan the new code with a
    different device. The new device shows the kitchen's page. The old tablet stops working: reload it
    and it says it is no longer set up. Nothing was lost, because the orders were never on the device.

    **The same tablet scans another station's code.** Take the tablet that is set up for the bar, tap
    "Tablet einrichten" on the kitchen and scan that code with it. It lands on the kitchen's page, and
    on the laptop the bar's row goes back to saying that no tablet is set up, because one device
    belongs in one place. Do not reload the tablet once it lands. Send an order to the kitchen from a
    phone and watch it appear on the tablet on its own, which is what shows that the tablet is now
    listening for the kitchen instead of still for the bar. Set the bar up again with a tablet of its
    own before you go on, so the rest of the walkthrough has the two tablets it expects.
21. **Place an order that splits, with two different answers.** The phone opens on the two categories,
    each one a wide button in the colour you picked for it. Tap Speisen, add one of the kitchen item,
    then tap "Hinweis" beside it and write something the kitchen needs to know, for example ohne
    Zwiebeln, which puts a second one on the order carrying that note. Tap the button back to the
    categories. The Speisen button now reads "2 x Speisen". Tap Getränke, add three of the bar drink,
    and go back the same way. Then type a table name, and go to
    the summary. There are two cards, one
    per station. Leave the kitchen on "Gesammelt ausgeben" and switch the bar to
    "Einzeln ausgeben". The kitchen
    card now carries a waiting time on its "Gesammelt ausgeben" button, built from the
    preparation minutes standing in front of it, and the bar card carries none, because its items go
    out one at a time. Scroll the summary up and down and watch the total and both send
    buttons stay along the bottom the whole time. Tap "Bestellung senden". Nothing goes out yet. The
    question lists the table, the amount, and one line per station saying what that station will do
    with its part. Tap "Abbrechen" and check that the summary
    stands exactly as you left it. Tap "Bestellung senden" again, confirm it this time, and
    read the confirmation with its order number.
22. **Watch it arrive.** Without touching either tablet, the kitchen's page gains one card in the left
    hand column, "Bestellungen, die zusammen rausgehen", carrying the order number and the kitchen's
    own number for it. The bar's page gains three separate items in the right hand column, "Positionen,
    die rausgehen, sobald sie fertig sind". Neither tablet was reloaded and neither shows the other
    station's work.
23. **Advance one item.** On the kitchen tablet, start one of the two items. It reads "in Zubereitung"
    and the other still reads "wartet". Mark that one ready. It reads "fertig" and stays on the card,
    because the card goes out together and the person reading it needs to see what is done.
24. **See the table name.** Marking something ready shows the table on the tablet, with the sentence
    telling somebody to write it on the tray. Nothing happens on the phone: no sound, no banner, no
    notification anywhere. That is the intended behaviour and this step exists to prove it.
25. **Advance a whole card.** On the bar tablet, mark one of the three drinks ready. It leaves the
    screen immediately, because it goes out as soon as it is ready, and the other two stay. On the
    kitchen tablet, use the card's own button to move everything left on the card in one tap. When the
    last item is ready the card leaves the screen.
26. **Ready is final.** Look for any way to move a ready item back on either tablet. There is none.
27. **The open tables overview.** On the phone, open the open items screen. The table is there with
    the five lines it still owes for, each one listed with the order number it came from and the
    price the guest was quoted, and the one you wrote a note for carries that note. Tap "Liste neu
    laden" and the same five lines come back, which is what the screen's own notices tell a waiter
    to do when they cannot be sure what reached the laptop.
28. **Settle.** Select one line of the table and tap the settle button, which sends the full price of
    what you selected and asks nothing further. That line disappears and the open amount drops.
    Select another line, tap the second button, type 0 as the amount, and try to confirm before
    you have typed a reason: the confirming button does nothing. Type the reason and confirm. The
    table's section for what was not collected names the reason and the amount. Select two of the
    three lines that are left, tap the second button again, enter an amount smaller than what they
    cost together, type a reason, and confirm. Those lines disappear as well, and the part nobody
    paid shows up in that same section, which is the table that hands over twenty euro on a bigger
    tab. Settle the last line with more than it costs, which needs no reason, because a guest is
    allowed to round up.
29. **The gap check.** Place three more orders to the kitchen and read the kitchen tablet's numbers.
    They run consecutively. That run is the whole loss detection mechanism and it is worth seeing once
    with your own eyes.
30. **A second festival counts from one.** In Feste, edit tonight's festival so that its end lies in
    the past. The phone's menu goes empty and the kitchen tablet says that no festival is active. Now
    click "Kopieren" on that same row and give the copy a period that covers right now. The copy
    arrives with the same stations, the same items and the same prices. Open it and place an order
    from the phone: it carries number 1, and at the kitchen it is number 1 as well, while the orders
    of the first festival keep the numbers they had.

31. **Something disappears while a waiter is holding it.** With items already in a waiter's basket,
    go to the laptop and mark one of those items sold out, switch a second one off, and switch off
    the whole category a third one came from. Leave the phone standing inside that category while you
    do it: without a reload it goes back to the list of categories on its own, and everything already
    on the order stays on it. Then look at the phone without reloading it, and at the summary. All
    three lines are marked there. The sold-out one still shows its price and still counts toward the
    total, because the laptop still names a price for it. The two whose items have left the menu show
    no price at all and add nothing to the total, so the figure you would read out is the figure the
    laptop would record. The two send buttons have left the strip at the bottom, and "Nicht
    bestellbare Artikel entfernen" stands there in their place. Tap it: the marked lines go, the
    total drops to what is left, and the send buttons are back. Write down what you saw, because a festival is the wrong place to find
    this out.

32. **A tap that arrives while the list is still moving.** Inside a category with more items than fit
    on the screen, flick the list hard and put your finger straight back down on the button at the
    bottom to stop it. Nothing happens, which is what should happen. Wait half a second and tap it
    again, and it answers at once. Do the same on the summary against the send buttons, where an
    accidental tap would start the order on its way.

    While you are there, take the second order of the evening as far as a category and use the
    phone's own back gesture. One press closes the category and shows the categories again, and a
    second press on the categories leaves you where you are rather than taking you out of the app or
    back to the summary you already sent.

33. **An order that never reaches the laptop.** Build a full order on a phone and go to the
    summary. Switch the phone's WiFi off, tap "Bestellung senden" and confirm the question that
    follows. The button reads "Wird gesendet" and the phone gives up after about ten seconds: a red panel says the laptop was not
    reached, and the strip at the bottom now carries "Erneut senden" where the two send buttons
    stood. Time that pause once with a watch, because it is the whole point of the limit. Without
    it the phone would stand there for a minute waiting for the operating system to lose patience,
    and a waiter holding a phone that says "Wird gesendet" for a minute assumes the order went out.

    Now try to change that order, on both screens. On the summary the way back to the items, the
    delivery choice at every station, and the button that removes items which cannot be ordered are
    all dead, while the lines and the total are still there to read and to copy. Then tap
    "Bestellung aufnehmen" in the header, which is what a waiter reaches for when the next table
    waves: the phone puts you back on the summary instead of opening the items, because a frozen
    order is unfinished business and the summary is where its two ways out are. That freeze is the
    point of the step: the laptop may already hold the order exactly as it was sent, so a changed
    order sent again would come back as the old one and the change would vanish without anybody
    being told.

    Now reload the page with the WiFi still off, the way a waiter does when a screen looks stuck.
    The order comes back in full and still frozen, and the panel now says the page was reloaded
    while the order was on its way and the phone does not know whether it arrived. A reload is not a
    way out of the freeze, and it must not be, because the laptop may hold the order either way.

    Switch the phone's WiFi back on for a moment, mark one of the ordered items sold out on the
    laptop, wait for the phone to flag it, and switch the WiFi off again. The line is flagged on the
    summary and "Erneut senden" is still tappable, because a retry asks what became of an order that
    may already exist.

    Tap "Erneut senden" with the WiFi still off. A dialog covers the screen and cannot be tapped
    away: no cross, and a tap beside it does nothing. Its heading reads in full, wrapping onto a
    second line rather than breaking off mid-word, and its two buttons sit one above the other across
    the full width of the card with their labels complete. It tells you to write the order on a slip
    of paper and to look at the tablet at the station. Switch the WiFi on and tap "Noch einmal
    versuchen": the order goes through and the phone is back at the items with an empty order. Stage
    the two failures once more, leave the WiFi off, and tap "Bestellung ist aufgeschrieben": the
    order is cleared, a fresh one is started, and the tablet at the station is what tells you whether
    your slip is needed.

    Now the other kind of failure, the one where the laptop answers and the answer is no. Take one
    more order with a single item on it, switch the WiFi off, and tap "Bestellung senden" and confirm it, so that
    the order freezes the way it did above. With the WiFi still off, go to the laptop, open that item and
    move it to a station it is not on yet, ticking the new one and unticking the old one so the item
    still has exactly one place to go. Switch the phone's WiFi back on and look at the summary
    without reloading it. The line is marked, saying that this station no longer prepares the item,
    and the button that would clear it is dead, because the order is frozen and the laptop may
    already hold it. "Erneut senden" is still tappable, so tap it.

    The laptop answers, and its answer is no, and the dialog about paper covers the summary straight
    away. That is what this step is for. The order is still frozen by the attempt the laptop never
    answered while the WiFi was off, so the item the laptop named cannot be taken off it, and tapping
    the retry again would fetch the same answer back. Behind the dialog the red panel says that this
    station does not prepare the item. The dialog's own first sentence still speaks of a laptop that
    was not reached, which is not what happened here, and it stays as it is because the panel behind
    it names the real reason.

    So put it right where it can be put right, which is the laptop: tick the station the item was on
    before and untick the other one again. Tap "Noch einmal versuchen" on the dialog. The order goes
    through, the confirmation carries its number, and the phone starts an empty order. Acceptance is
    one of the two ways out of a freeze that a silence started, and "Bestellung ist aufgeschrieben" is
    the other.

34. **An item moves to another station while a waiter is holding it.** This is the same change as the
    one you just staged, seen by a phone that is not frozen, and the two results are meant to differ.
    With the WiFi on and nothing frozen, take a fresh order with one drink on it and leave the phone
    standing on the summary. On the laptop, open that drink and move it from the bar it is on to the
    other one. Without a reload, the phone marks the line under that bar's heading, saying that this
    station no longer prepares the item. The two send buttons have left the strip at the bottom, and
    "Nicht bestellbare Artikel entfernen" stands there in their place. Tap it, go back to the items
    and add the drink again:
    it goes to the other bar without asking, because that is the only bar left, and the order
    sends. The laptop would have refused this order, and the point of the step is that it never had
    to.

35. **The question about which bar makes it, and backing out of it.** On the laptop, tick both
    stations on one drink so either can pour it. On the phone, tap that drink: the phone asks which
    of the two makes it. Tap "Abbrechen". Nothing is added to the order, you stay in the category,
    and the question does not come back when you open another category. Tap the drink again and
    answer properly this time: the line names the bar you chose, and only that bar sees it.

36. **A phone that was set up again while its waiter was out of range.** Take a fresh order on a
    phone, with two or three items and a table name on it, and leave it standing on the summary
    without sending it. Switch that phone's WiFi off, which is the phone being carried out to the far
    end of the site. On the laptop, open the waiter list and tap "Telefon neu einrichten" on that
    waiter's row, the way you would for somebody who has handed their phone to the next shift. Leave
    the code standing on the screen and do not scan it yet.

    Switch the phone's WiFi back on, tap "Bestellung senden" and confirm it. The phone does not tell you that the
    laptop could not be reached, because it was reached: it shows the screen that asks for the phone
    to be set up, with the notice that the order you had started is still there. That notice is the
    step: an order a guest gave a waiter is not thrown away because somebody tapped a row on the
    laptop.

    Now scan the code that is still on the laptop. The phone comes back to the items with your order
    still in the basket, and the summary is the one you left, down to the table name and every line.
    No red panel stands on it, because the laptop turned the phone away before it read the order and
    the phone wrote nothing down about it. Had an earlier attempt on this order gone unanswered, the
    sentence about that attempt would still be standing here, for the same reason. Send the
    order and
    watch the station tablet: exactly one order arrives, with one number, because the order carried
    the same identity through the whole of this and the laptop recognises it by that identity alone.

## After the walk

The backup button and the diagnostics page described in the specification are not built yet, so these
last two steps use what the program window actually offers today.

37. **Take the data off the laptop.** Click "Open the data folder" in the program window and copy the
    whole folder onto a stick while the program is still running. Copy `gastronomy.db` on its own as
    well, then open both copies on another machine: the whole folder holds the evening's orders, and
    the single file may be missing the last of them. That difference is the reason the backup belongs
    behind a button, and seeing it once is what makes the point stick.
38. **Read the log.** In that same folder, open the newest file under `logs`. The enrolments, the
    startup and the port are all in it, and no device token and no enrolment code appears anywhere in
    it.
