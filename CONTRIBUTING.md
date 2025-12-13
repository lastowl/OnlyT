# How to contribute

Thanks for thinking about contributing to this fork of OnlyT! If your changes are likely to be more than a few lines of code, please let us know in advance what you propose to do by opening an issue. This will help avoid duplication of effort.

## Submitting changes

Please send a Pull Request with a clear list of what you've done (read more about [pull requests](http://help.github.com/pull-requests/)). 
When you send a pull request, please follow our coding conventions and make sure all of your commits are atomic (one feature per commit).

Always write a clear log message for your commits. One-line messages are fine for small changes, but bigger changes should include a more comprehensive description.

## Localisation

This fork manages translations directly in the repository (not via Crowdin). If you want to contribute translations:
- Edit the appropriate `Resources.xx.resx` file in `OnlyT.Avalonia/Properties/` or `OnlyT/Properties/`
- Ensure the key names match the base `Resources.resx` file
- Submit a PR with your translation changes

## Coding conventions

A StyleCop ruleset file is used to help maintain consistent coding conventions. 
The projects reference the StyleCop.Analyzers assembly so it should be possible to stick to the coding style without too much difficulty.

Many thanks for contributing!
