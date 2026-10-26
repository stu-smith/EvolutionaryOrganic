Morphogen
=========

Breed strange, organic 3D sculptures -- horns, shells, flowers, crowns and
creatures -- through aesthetic selection, in the spirit of William Latham's
Mutator and FormSynth. You don't design the forms; you choose the ones you
like, and evolution does the rest.

Running it
----------

Double-click Morphogen.exe. Nothing is installed and nothing is written to the
registry -- this build carries its own copy of the .NET runtime, so it runs on
a clean Windows 10 or 11 machine with nothing set up first. It does need a
Direct3D 11 capable graphics card.

Using it
--------

The window shows a 3x3 grid. The current creation sits in the centre,
surrounded by eight mutants of it.

  Select      make a mutant current and breed a new generation around it
  Re-roll     discard the mutants and breed eight fresh ones (F5)
  Keep        add a creation's genome to the project, listed on the left
  Combine     cross a kept genome with the current creation

Rotate a creation with left-drag, zoom with the mouse wheel or right-drag, pan
with middle-drag or Shift + left-drag. Double-click a view to reset it.

Shortcuts
---------

  Ctrl+Shift+N    new project
  Ctrl+O          open project
  Ctrl+S          save project
  Ctrl+Shift+S    save project as
  Ctrl+N          new random creation
  Ctrl+I          import genome into project
  Ctrl+E          export current genome
  Ctrl+P          export high-resolution image
  Ctrl+Z          back to previous selection
  F5              re-roll mutants

Where your settings go
----------------------

  %AppData%\Morphogen\settings.json    preferences

Plain JSON and safe to edit or back up by hand. Your projects (.morphogen.json),
genomes (.genome.json) and images live wherever you saved them, not in here.

Uninstalling
------------

Delete this folder. Optionally delete %AppData%\Morphogen too. Your projects,
genomes and images are untouched either way.

More
----

https://github.com/stu-smith/EvolutionaryOrganic
