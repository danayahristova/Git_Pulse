# VHS script for the README demo GIF: https://github.com/charmbracelet/vhs
# Run it from inside a repository with some history:   vhs path/to/GitPulse/docs/demo.tape
# Needs gitpulse on PATH (dotnet tool install --global GitPulse).

Output docs/demo.gif

Set Shell "bash"
Set FontSize 14
Set Width 1280
Set Height 800
Set Theme "GitHub Dark"
Set TypingSpeed 60ms

# Warm the cache so the demo isn't mostly a progress bar.
Hide
Type "gitpulse scan > /dev/null && clear"
Enter
Sleep 10s
Show

Type "gitpulse overview"
Enter
Sleep 5s

Type "clear && gitpulse heatmap"
Enter
Sleep 5s

Type "clear && gitpulse hotspots --top 5"
Enter
Sleep 5s

Type "clear && gitpulse commit HEAD --no-diff"
Enter
Sleep 4s

# Interactive mode: open the menu, look at the authors, then exit.
Type "clear && gitpulse"
Enter
Sleep 6s
Down
Enter
Sleep 4s
Type "Exit"
Enter
Sleep 1s
