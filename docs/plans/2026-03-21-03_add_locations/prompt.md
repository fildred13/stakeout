Let's add basic location support to the simulation, as well as the player themselves as an entity within the simulation.

Locations will be broken down into a few layers:

- Country
- City
- Street
- Address
- Floor
- Sub-Location

Country and city are self explanatory. For now, we won't use these much, as we'll start development focused on activities in a single city. Let's default to Boston, United States. That will the Player's starting Country and City, and the simulation debug will run within that city.

Streets are what tehy sounds like. They have a name, and each street exists in exactly one city.

Addresses are what they sound like. They are linked to exactly one street, and have a number associated with them, resulting in a final string representation of someting like 42 Main Street. Each address exists at an x,y coordinate within the city. Addresses can be thought of as discrete locations where an event may occur, and thus has a location type. A person's suburban home, a diner, a park, an office, etc. Essentially, it is either a "lot" which contains either buildings or outdoor areas. We will progressively add new types of locations, each with their own generation parameters etc.

Floors are for multi-floor locations such as an office building, or a two-story residential home. All addresses have at least a ground floor. Some have basement levels (B1, B2, B3), or upper floors (2, 3, 4, etc.)

Sublocations are generally "rooms" or exterior areas within locations. For a home, some of the sublocations may be Bedroom, Bathroom, Kitchen - but also things like Backyard, Front yard, road, etc. For a park, sublocations might be pond, utility shed, west trail, north trail, east trail, north field, etc. Sub locations all related to a single parent Location. Sub locations have connections to one another. For example, in our suburban home location example, Road connects to Front Yard. Front Yard connects to Road and to Backyard and to Hall 1. Hall 1 connects to Kitchen and Bathroom 1 and Bedroom 1. Bedroom 1 connects to bathroom 2.

As you can see, floors and sublocations are by far the most complicated part. Let's leave out the actual floor and sub-location stuff for now. I mention it only in case it helps you to think about the final data structure. We'll work on procedural generation of locations later.

Let's plan to add Country through address for now.

Supported Countries:
- United States

Supported Cities:
- Boston

- Create a data pool of random U.S. street names as a placeholder for streets.

- Addresses should use a number between 1 and 10000, with a skewed bellcurve centered on 200. Supported address types:
- Suburban Home (residential)
- Diner (commercial)
- Dive Bar (commercial)

Additionally, we need to expand the debug mode to see that things are working. 

Whenver we generate a person, the simulation should also generate a home location for that person (from among residential options), as well as a work location (from among commercial options).

So that the player has a location as well, let's generate a suburban home location for the player at the start of the game. This will necessitate that the player, as well as all Person entities, have a current location attribute added to them and maintained by the engine at all times.

Let's change the debug screen to show a primary view which is a map of the current city. Let me know where to add .png file to use as the placeholder background texture for the city map.

This map view should show a little square icon with rounded corners for each location. When mousing over any of these icons, a small hover text should appear indicating the Address of the hovered location, as well as it's type (Suburbran home, Diner, Dive Bar, etc.)

The map view should also show a small circular dot for each Person and Player entity. When hovering the mouse over these dots, the hovertext should show the name of the person.

This is a fairly big plan. Be sure to write a step-wise plan, and make sure that the plan includes notes to write changes to changes.md between each step in case we become interrupted during coding.