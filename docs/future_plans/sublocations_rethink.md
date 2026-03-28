Let's use your brainstorming superpower to think about a major refactor.

Having now toyed with our prototype for awhile, I am thinking that we vastly overcomplicated the sublocation feature, and by extension the scheduling. I think giving the player a map of every location and having to track the NPC movement throughout is a huge amount of development cost and complexity that doesn't enhance the player's experience enough to warrant it.

What are the major ways NPCs need to move around? They need to be able to travel between cities, once we have bigger plots like terrorist threats etc.

Within cities, they need to be able to move between addresses. Home to work to diner to home, or Hideout to Alley to Bank to hideout, stuff like that.

Within addresses, what does the game really need to know in order to provide a rich user experience? For simple locations, like suburban homes, we really just need to have a simple list of sublocations in the home, having attributes like "Person Foo's Bedroom" and whether or not that room contains key objects, like Person Foo's Computer (which you would someday be able to hack), as well as what traces have been left in that room (more on that later).

For more complicated addresses, like apartment buildings, we probably need to be able to recurse into sublocations. This is best with an example. Let's say a user arrives at an apartment building... what options would they see? Let's go through their flow. In each screen we'll describe the art the player is seeing and what is filling the left hand sidebar:

Address screen:
---
Art: graphic of exterior of apartment building

You are outside of 123 MAIN STREET. This is a 7 story apartment building. It looks wealthy. There are 10 cars parked in the parking lot.

Enter
Investigate outdoor parking lot
Conduct stakeout
Leave
---
In this screen we see a few things happening. We see the address itself is bolded (shown in all caps here). This means this text is right-clickable to interact with it to add it to evidence board. It would show in red text if it is already added to the evidence board, otherwise in white text.

The facts of the building which are visible from the exterior are described textually. I am imagining that there are a set of sentence fragments of observations which get concatenated together based on the state of the world. First sentence is always what address you are at. Next sentence describes the lot type and number of stories. Then optional sentences as we add more details to the game, like the "wealthy" sentnece might be based on something like high address Wealth in the future. If it has a street level  parking lot, it says something about the number of cars parked there. If there were visible broken windows or a smashed down front door, it would say something about that, if the place is obviously guarded by bad guys, it would say something about that, etc.

The player also sees a list of actions they can take. In this case they see "Enter" because it is an unguarded, public location. If it was not open to the public, it would say Break In. If it was guarded it would say Infiltrate. You can always stakeout a location to watch for who comes and goes, etc. In this case, this particualr apartment building has a parking lot that is publicly_accessible and `exterior`, so there is an Investigate outdoor parking lot option.

Once a player enters, they see the following screen:

Interior screen:
---
Art: art of a snooping detective.

You are inside 123 MAIN STREET, a 7-story apartment building.

Investigate public spaces
Break into security
Go to an apartment
Leave
---
Each address type will have it's own composed list of options. For example, every apartment building will have "Go to an apartment". Every apartment building will have "investigate public spaces" (for things like looking for fingerprints on elevators, discovering that there are mailboxes, etc.). Only apartment buildings that have a security system will have the option to break into security. Only apartment buildings with a parking lot will have investigate parking lot. Notice that many building types may have security rooms, so that would be re-usable. Many addresses may have an external parking lot from the last screen, so that would be reusable, etc.

If the player chooses to break into an apartment, then they are presented with the following screen:
Eelvator screen:
---
Art: interactable elevator panel with buttons to select floor, and a depiction of an elevator door

Nevermind
---
"Nevermind" is the "go back" option. Otherwise, the player clicks a floor, which causes the elevator to rumble for a few seconds, and then DING, and then the doors open, revealing the following text:
---
Break into which unit?

2A
2B
2C
2D
2E
2F
---

Once the user selects a unit, if they have the key and know that it belongs to this door, then the game skips past this screen. If not, then they are now challenged by the door.
Break in screen:
---
Art: closed door

The door is locked. You don't hear anyone inside.

Knock
Try keys
Pick lock
Break down door
Dust for prints (5 minutes)
Nevermind
---
Knock allows the player to see if anyone is home, who may or may not answer, and if they do it starts a conversation. Try keys allows the player to autoamtically try all unknown keys in their possesion on the door, and displays if they have any keys. Pick lock appears if the player has a lockpick kit, and opens the lockpicking minigame (once implemented). Break down door is always available, but is loud and if anyone is around to hear it, the cops will get called, which will be a problem for the player - it puts them on the clock. Nevermind is the Go-back option and takes you back to the elevator screen. Options would be different if the door was unlocked or broken down, of course.

Let's say the player dusts for prints. Then 5 minutes instantly pass, and then a modal appears showing a list of discovered fingerprints. The player can right-click any of these to add them to evidence board. Every fingerprint is given a unique identifier, so if you don't know whose fingerprint it is, it shows up on the evidence board like "Fingerprint D", "Fingerprint AX", etc. If the player has already associated a fingeprint with a person by way of incontrivertible fact (to be implemented later) and they know the owner's name, then the fingerprint would instead display as "John Smith's Fingerprint". The player can use a dismiss button to dismiss the modal.

Then the player decides to try keys. Let's assume they have the key, so they simply enter the apartment. They are now presented with a new screen:

Apartment Screen:
---
Art: snooping detective

You are in UNIT 2B of the apartment building at 123 MAIN STREET. There is no one here.

Investigate
Leave
---
Leave is obvious.

Clicking investigate switches to the investigate screen:
---
Main screen content:
Investigating...

sidebar content:
Stop
---
Investigating takes time, which can be a precious resource for a player. The elipses keeps blinking, indicating that the player is investigating. They can accelerate time as needed. Bit by bit, evidence will be discovered. It shows up as a list that keeps having items appended to it as the player discovers things, for example:

Found a computer in the office. (as soon as this item appears, Hack into Office Computer appears as an option in the sidebar, and remains in the sidebar every time the player returns to this apartment - they now have this piece of knowledge and do not need to discover it again.)
Found FINGERPRINT AX all over the apartment.
Found RECEIPT for JOE'S DINER in the kitchen trash.
Found CIGARETTE BUTTS in Master Bedroom.
Found John Smith's Diary in Master Bedroom.

And so on. As with other places text is displayed, things that can be added to evidence board are bolded, and colored red if already on the evidence board.

Under the covers, the game knows all the traces, facts, and clues discoverable in the apartment. Every trace has a difficulty that weights how likely the player is to discover that piece of evidence, and the game automatically increases the chances of discovering evidence more quickly if it is related to any facts known on your pinboard. We'll think through that algorithm when we get there.

If the player decides clicks hack into computer, then the screen switches to the hacking minigame, and investigation is paused (but time keeps ticking! while the player does the hacking minigame and then starts exploring files on the computer etc.)

I think that gives a pretty good idea of how I'm imagining the physical interaction with addresses will work. At no point did the game need to tell the player about how rooms were connected together - it's kept vague enough that the player's imagination and understanding of these places is good enough. The experience is streamlined and focused on getting access to the place with the clues, and then using "investigate" to search for clues there, with a few minigames to slow you down.

# Simulation aspects

With a better understanding of how the player will interact with the world, we now can turn our attention to the simulation itself.

Today, we schedule each person's lives both at the city layer (which address am I going to and what will I do when I am there?) but also the address layer (while I am at this address, what room am I in at any given moment?) They leave traces through every minute interaction - physically passing through each door leaves fingerprints, for example.

I think we can simplify that quite a bit.

What does the game really need to know in order to enable the player experience described above?

Certainly, it needs to know about cities (when we eventually add multiple city travel), the map of each city, the addresses in each city, the locations within an address (in our example, security room, exterior parking lot, unit 1a, unit 2a, unit 2b, etc.) and the sublocations in the instances where that is necessary (like the rooms of an apartment unit, the floors of an office building, etc.). Most addresses will just have direct sublocations, like a simple diner. So the layers become:

World
City
Address
Location
Sub-Location

If a person lives in a place, then it can pre-populate all sorts of traces by way of that FACT, rather than through interactions. It is simply a fact that a person's home is filled with their fingerprints. It is a fact that a person's front door will likely have their fingerprint, etc.

When something unusual happens, like a killer coming to a home, then a series of traces can be generated and left. For example, if the game decides that a killer breaks in the through a window, charges to the person's bedroom, a scuffle breaks out, the person gets stabbed and runs to the kitchen and grabs a knife, another scuffle happens and the killer gets stabbed, the killer then kills the victim and leaves through the front door, then all sorts of traces can be generated:

Witness events of neighbors who saw him creeping outside the window, witness reports who heard the glass smash, killer's fingerprints on the outside of the window, bedroom is smashed up, a few fingerprints of the killer's on the wall of the bedroom (during the fight), blood trail between bedroom and kitchen, corpse in kitchen, kitchen is smashed up, pools of blood in the kitchen (victim and killer), bloody handprint on inside of front door handle, fingerprint on the door handle, blood trail from kitchen to front door, blood trail from front door to road.

All of that can be generated as part of the actions occurring without needing to actually track the physical location of the killer and person at the room level - just knowing they're both in the same location and sublocation is good enough, and the crime template can contain.

This isn't just true of crimes. For example, let's say a  woman goes to a man's house on a date. She might leave a trace of "a few fingerprints in bathroom 1", "a few fingerprints in the kitchen", "a few fingerprints in the living room". We don't need to track all of her movements through the house, the "visit on date" action can generate traces like that which fade over a few days.

Which brings us to the meat of the challenge: a full re-think of the NPC simulation layer and how we define an extensible, composable system for declaring a library of templated, variable actions with their corresponding trace generations. We need to simplify, focus on being able to reliably add many variant action templates, reliably and efficiently schedule NPC days, allow for NPC "grouping" and "ungrouping" (two people on a date meet up, then they're moving and doing things together, then they un-group. Ditto a group of criminals meeting up to start a heist), allow for spawning people with a history (reversing their schedules and "backfilling" their traces into the world), and easily account for interrupts and dynamic interaction between the NPCs and other NPCs, as well as the player.

Use your brainstorming super powers to think this through in a lot of detail. Act like an expert game designer and software engineer thinking about both the user experience as well as technical implementation options. Let's capture the ideas in a much more detailed future_plan that lives alongside this initial idea document. We'll iterate on that design document together, and then in a future task (not now) we'll work on decomposing it into pieces so that we can execute a series of projects to get us from vision to implementation.


