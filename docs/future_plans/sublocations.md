At present, the model for locations is essentially just an address and an address type. For example:

123 Main Street, Suburban Home
456 Fantasy Lane, Office
78 Smith Avenue, Park
9 Example Street, Dive Bar

I chose these examples because they illustrate quite the gamut of different locations: 

One is a simple suburban home with one or two stories, plus maybe a basement. They probably have a front yard, maybe a backyard, they have a front door, a backdoor, maybe a bulkhead. They have ground-story windows and second story windows, and maybe basement windows. They may have garages. They have a kitchen, some number of bedrooms and bathrooms, hallways, stairwells, studies, libraries. They may vary depending on the wealth of the address  (in the future).

One is an office. Let's assume the address is a high rise. An office has a building lobby, a security room, a utilities area, maybe a sub-level parking garage, an elevator, a stairwell, reception area, possibly a few cubicles areas, a few offices for executives and managers, a women's room, a men's room, a break room. The lobby is attached to a hall which is attached to security. The hall is also attached to elevators and the stairwell. The stairwell is attached to reception, reception is attached to the cubicles area, the offices are attached to cubicles area, etc.etc.etc.

The park is outdoors, and maybe has a small beach along the shore, a jogging path, a central woods area, a parking lot, a baseball diamond.

The dive bar has a front door and back door. A storage room in the back. A small office for the manager. The main bar area. An alley with a dumpster where employees park, etc.

Critical to the detective fantasy is the idea of exploring a crime scene. Critical to the spy fantasy is snooping around places you aren't allowed to be - secret rooms, security rooms, hidden drug dens, etc.

We need to build that model of sublocations into the data model early, so that the task system can hook into it, so Traces can be left at not just addresses, but also sublocations. This means codifying essentially "rooms" or "spaces" and how they connect to one another and the outside world. One ideas is that each address can be thought of as a tree of nodes that all start from a Road node, that represents the road in front of the address. Then there are a series of "rooms" or "sublocations" that connect to the Road node via different access methods, e.g.:

Road
Building Lobby connected to Road by Front Door
Elevator connected to Building Lobby by Elevator Doors
Office reception connected to Elevator by Elevator Doors (how do we track which floor the office is on? For that matter, how do we track how many floors a building has? How do we track that fact that there may. This gets more interesting for something like a large residential building - how do we have multiple floors so that even if you know a killer lives at 123 Fantasy lane, you have no clue which room they live in because there are 20 floors with 15 rooms on each floor?)
Security Room connected to Building Lobby by Door.
etc.

I'm less sure how to do that for something like a Park, which is an outdoor space. I'd like to see some ideas around this.

All of this will need to tie in both to the NPC action system, as well as to the eventual way that the Player is able to interact with the spaces. It would be pretty cool if we had something like "blueprint views" of each location - overhead views of whatever floor of a building the player is currently on. The player's current room is highlighted, and the player can click on rooms to see possible context menu options like "go to" and, once in a room "tear the room apart to look for clues (10m, Loud)", "carefully search the room, leaving no trace (45m, Quiet).".

I've been debating like crazy whether or not I want to do actual maps vs text-based methods of interacting with locations. I think the best thing to do is to prototype both. The underlying generation parameters (rooms/sublocations connected by access means) works for both systems (I think - please let me know if I'm missing something). I think if we offer the player both ways of interacting with spaces, I can play both ways for the same locations for awhile and start to feel out which is more fun/worth it (maps are almost certainly more work, but it's unclear if they're more fun, equally fun or, counterintutively, LESS fun because they're tedious and detailed. I doubt it, but I want to see.)

I do NOT want to get bogged down in detailed art of each room/location. Basic maps that visualize a floor of a space is what that will be, and we'll still have textual descriptions of what you see in each room etc.

So let's brainstorm what it would look like for the player to enter a location in "map mode" to do things like interview witnesses and investigate for clues, etc. Part of testing this feature will be to have the player enter a few locations and look at the maps, and navigate between floors.

Meanwhile, for text based interaction, I think we can delay adding anything just yet. In the next feature we build, we'll add the beginning of Traces, and that will allow us to try out this new system for investigating a murder.

One other "definition of done" for the current feature will be that I can watch the Person Inspector view to watch people be in various rooms/sub-locations, not just in addresses as a whole.