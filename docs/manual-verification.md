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

17. **Set the menu up.** In the admin pages create two stations, for example Küche and Theke. The
    overview then asks for a category, because an item cannot be created without one. On the
    Artikel page tap "Neue Kategorie" twice, for example Speisen and Getränke, and pick a colour for
    each one. The heading of each category is written on that colour, and the lettering stays readable
    on a light colour as well as on a dark one. Move Getränke above Speisen with the arrows beside its
    heading, and the two headings swap places. Neither category is on the phones yet, because a
    category with no items in it, or with all of its items switched off, does not appear on a phone
    at all. Create one item in Speisen that takes a while, with a preparation time in minutes,
    assigned to the kitchen only. Create one drink in Getränke with the preparation time left
    empty, assigned to the bar only. The overview now asks for one thing only, and it asks it once
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
21. **Place an order that splits, with two different answers.** The phone opens on the two categories,
    each one a wide button in the colour you picked for it. Tap Speisen, add two of the kitchen item,
    and tap the button back to the categories. The Speisen button now reads "2 x Speisen". Tap
    Getränke, add two of the bar drink, and go back the same way. Then type a table name, and go to
    the summary. There are two cards, one
    per station. Leave the kitchen on "Zusammen" and switch the bar to "Sobald fertig". The kitchen
    card shows a waiting time built from its preparation minutes, and the bar card does not, because
    its items go out one at a time. Scroll the summary up and down and watch the total and both send
    buttons stay along the bottom the whole time. Send the order and read the confirmation with its
    order number.
22. **Watch it arrive.** Without touching either tablet, the kitchen's page gains one card in the left
    hand column, "Bestellungen, die zusammen rausgehen", carrying the order number and the kitchen's
    own number for it. The bar's page gains two separate items in the right hand column, "Positionen,
    die rausgehen, sobald sie fertig sind". Neither tablet was reloaded and neither shows the other
    station's work.
23. **Advance one item.** On the kitchen tablet, start one of the two items. It reads "in Zubereitung"
    and the other still reads "wartet". Mark that one ready. It reads "fertig" and stays on the card,
    because the card goes out together and the person reading it needs to see what is done.
24. **See the table name.** Marking something ready shows the table on the tablet, with the sentence
    telling somebody to write it on the tray. Nothing happens on the phone: no sound, no banner, no
    notification anywhere. That is the intended behaviour and this step exists to prove it.
25. **Advance a whole card.** On the bar tablet, mark one of the two drinks ready. It leaves the
    screen immediately, because it goes out as soon as it is ready, and the other one stays. On the
    kitchen tablet, use the card's own button to move everything left on the card in one tap. When the
    last item is ready the card leaves the screen.
26. **Ready is final.** Look for any way to move a ready item back on either tablet. There is none.
27. **The open tables overview.** On the phone, open the open items screen. The table is there with
    what it still owes. Each item says where it is in production and names its station, and says
    whether it comes with the rest of the order or on its own. Advance an item on a tablet and watch
    the phone's line change without a reload.
28. **Settle.** Select part of the table and settle it at its displayed price. Those lines disappear
    and the open amount drops. Select the rest and settle it free of charge with a typed reason. The
    table's given-away section names the reason and the amount.
29. **The gap check.** Place three more orders to the kitchen and read the kitchen tablet's numbers.
    They run consecutively. That run is the whole loss detection mechanism and it is worth seeing once
    with your own eyes.
30. **Reset the numbers.** In the admin overview, reset the numbering and confirm. The next order the
    phone sends carries number 1 again, and the orders already taken keep the numbers they had.

31. **Something disappears while a waiter is holding it.** With items already in a waiter's basket,
    go to the laptop and mark one of those items sold out, switch a second one off, and switch off
    the whole category a third one came from. Leave the phone standing inside that category while you
    do it: without a reload it goes back to the list of categories on its own, and everything already
    on the order stays on it. Then look at the phone without reloading it, and at the summary. All
    three lines are marked there. The sold-out one still shows its price and still counts toward the
    total, because the laptop still names a price for it. The two whose items have left the menu show
    no price at all and add nothing to the total, so the figure you would read out is the figure the
    laptop would record. Both send buttons are now dead, with a line under them telling you to take
    the items off first. Tap "Nicht bestellbare Artikel entfernen": the marked lines go, the total
    drops to what is left, and sending works again. Write down what you saw, because a festival is
    the wrong place to find this out.

32. **A tap that arrives while the list is still moving.** Inside a category with more items than fit
    on the screen, flick the list hard and put your finger straight back down on the button at the
    bottom to stop it. Nothing happens, which is what should happen. Wait half a second and tap it
    again, and it answers at once. Do the same on the summary against the send buttons, where an
    accidental tap would have sent the order.

    While you are there, take the second order of the evening as far as a category and use the
    phone's own back gesture. One press closes the category and shows the categories again, and a
    second press on the categories leaves you where you are rather than taking you out of the app or
    back to the summary you already sent.

33. **An order that never reaches the laptop.** Build a full order on a phone and go to the
    summary. Switch the phone's WiFi off and tap "Bestellung senden". The button reads "Wird
    gesendet" and the phone gives up after about ten seconds: a red panel says the laptop was not
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
    away: no cross, and a tap beside it does nothing. It tells you to write the order on a slip of
    paper and to look at the tablet at the station, throwing the slip away if the order is already
    there and handing it over if it is not. Switch the WiFi on and tap "Noch einmal versuchen": the
    order goes through and the phone is back at the items with an empty order. Stage the two
    failures once more, leave the WiFi off, and tap "Bestellung ist aufgeschrieben": the order is
    cleared, a fresh one is started, and the tablet at the station is what tells you whether your
    slip is needed.

    Now the other kind of failure, the one where the laptop answers and the answer is no. Take one
    more order with a single item on it, switch the WiFi off, and tap "Bestellung senden" so that the
    order freezes the way it did above. With the WiFi still off, go to the laptop, open that item and
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
    station no longer prepares the item. Both send buttons are dead and the line under them tells you
    to remove the item. Tap "Nicht bestellbare Artikel entfernen", go back to the items and add the
    drink again: it goes to the other bar without asking, because that is the only bar left, and the
    order sends. The laptop would have refused this order, and the point of the step is that it never
    had to.

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

    Switch the phone's WiFi back on and tap "Bestellung senden". The phone does not tell you that the
    laptop could not be reached, because it was reached: it shows the screen that asks for the phone
    to be set up, with the notice that the order you had started is still there. That notice is the
    step: an order a guest gave a waiter is not thrown away because somebody tapped a row on the
    laptop.

    Now scan the code that is still on the laptop. The phone comes back to the items with your order
    still in the basket, and the summary still carries the table name and every line. No red panel
    stands on it, because the reason the laptop gave went with the setup the phone lost. Send the
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
