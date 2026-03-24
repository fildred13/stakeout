It's time to add the very basics of the star of the show: crime! It's not much of a detective game without some criminal activity!

Use your superpowers to plan and execute this iteration.

While almost all cases in STAKEOUT will be criminal in nature, some may be civil. The progression of the game will see you solving local, small, low-stakes cases at first, and eventually growing into an international super spy. docs/gameplay/progression.md shows the kind of progression the player will go through.

To prototype the system, we'll choose a mid-level crime: a simple murder by a serial killer. This is simple because the motive is pure ("I'm a serial killer, I just want to kill people.")

First, we'll need to add a system that injects new crimes. Rather than make that dynamic yet, let's add a new option in the debug menu that is simply "generate crime". This can then change that sidebar's content to show the "Crime generator" sidebar where you can pick options about the crime to generate. So far, there will only be one option: serial killer murder, and a button to Generate Now.

This is drawn from a pool of "crime templates", of which we only have one so far: serial killer. The options that can be tweaked in the generator come from options defined in the crime template itself. We need to brianstorm about how to manage these files, because future development of the game will revolves around creating many new ones. See later in this prompt for more information.

When the user clicks generate now, that activates a new component: the crime generator. I'm not super confident about the encapsulation of this component. I think in the future it may be able to generate none-crimes, such as a dog that runs away (which you may then be tasked with finding). I am also unsure if this same generator is something more of a general GoalGenerator that is able to generate arbitrary goals for NPCs. For example, imagine a goal an NPC has to plan and go on a vacation. This is useful in the context of the game because it creates things the NPC wouldn't normally do, which can act as confounding evidence or red herrings It simply makes the world feel more alive.

What's common is that they're multi-stage "objectives" that are composed of a series of goals which the NPC must complete and check off. Does that mean they should be the same thing? Should it be some sort of base class for the shared components? I can also think of differences: the game knows that crimes are way more important the confounding tasks, and needs to track all people/places/evidence etc. related to the crime. Am I overcomplicating this and we should just plan to iterate on non-crime goals later?

Whatever we decide, when the player clicks Generate Crime, the game instantiates a "crime template", which is essentially an injection into the simulation that will cause the crime to occur. A big part of developing the game in the future will be creating these templates in order to create unique scenarios. 

1. It generates a brand new NPC to be the serial killer (which, in turn, generates a home, work location, and additional people, per usual).
2. It 
