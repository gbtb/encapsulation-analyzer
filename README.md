# Roslyn Encapsulation Analyzer

This project uses Roslyn SymbolFinder to find public types in a project, which can be made internal. And it also provides simple refactoring, which indeed makes such types internal.
It is intended to help with encapsulating as much as possible of an inner (non-ui, library, business-logic) layers of your app.

## Motivation

Most C# IDEs by default configured to create new classes with public accessibility. And even if your IDE is not configured that way, other developers on your team may have other preferences.
It's probably not a big deal until you start to care about encapsulation and separation of various layers of your app. So this project aims to provide a tool for search and refactor such "unnecessary-public" types.

## How to use it

Currently this project has only a simple CLI interface. To analyze project run

`EncapsulationAnalyzer.CLI.exe analyze [path-to-sln-file] [project-name]`

It will display all found types in a table.

To automatically refactor found types, run

`EncapsulationAnalyzer.CLI.exe refactor [path-to-sln-file] [project-name]`

## Limitations and important details

* This tool does not have a reference cycle-detection mechanism. Therefore public types which cross-reference each other through properties or method arguments will not be detected as unnecessary public.
* This tool also does a single pass of reference search. Therefore it may require multiple passes of refactoring to hide all possible types in a project.

## TODO-list

## Possible improvements

[ ] Package cli as a dotnet tool

[ ] Allow to pick which types to refactor

[ ] Wrap encapsulation analyzer into CodeRefactoringProvider so it can be integrated into VS and/or Rider
