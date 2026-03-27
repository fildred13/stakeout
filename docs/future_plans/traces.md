Let's start to flesh out the traces system just a bit, along with some more complex actions and some supporting mechanics.

Most Actions that People execute in the game should start to leave traces. We'll zoom in on one, and then flesh out the systems around that.

When a person interacts with a door, they should leave a fingerprint on the handle of the door on the side of the door they are coming from. For example, a person walking through the front door of their house should leave a fingerprint on the front door.

In order to simulate other fingerprints rubbing off, for each other fingerprint on the front door, there should be a 25% chance to erase the fingerprint, which is increased by an additional 25% for every other fingerprint on the door. If this is generalized to all places where fingerprints are added to things, then we have a system which ensures that we don't have infinite fingerprints on surfaces.

But if a killer enters a home, they may not enter via the front door. For example a killer attempting to enter a suburbanHome may try to open a door to see if it is unlocked, try to pick the lock of a front door, may break the door down, may try to open a window to see if any are unlocked, may break a window, may try to go in the back door (leaving a fingerprint even if unsuccessful), may try to pick the backdoor lock, or may try to break down the door.

This involves adding several other mechanics as well.

Locations need avenues of ingress appropriate to their location type and sublocation type (front door, back door, window, etc.) - I forget where we are at on adding those.

We need to add a system for different people to use different types of ingress, (patrons use a front door of a business, employees may use a front door or back door, but for an Office things are completely different - employees will first go to a Lobby, followed by elevator (or stairs), leaving fingerprints on all doors and elevator buttosn along the way.

We need to add the concept of locked doors, and by extension keys. We need to have homeowners have an objective to lock their doors before going to bed, with a chance to forget to do so.

We need to add person inventories and location inventories, so that people can have keys, can leave their keys in a location when not in use, and can leave spare keys in certain places.

Keys themselves should have fingerprints left on them too, by anyone picking one up or using it, following the same fingerprint rules.