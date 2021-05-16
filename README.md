# Roslyn Encapsulation Analyzer

This project uses Roslyn SymbolFinder to find public types of a project which can be made internal. And it also provides simple refactoring, which makes such types internal.

## Motivation

Most C# IDEs by default configured to create new classes with public accessibility. And even if your IDE is not configured that way, other developers on your team may have other preferences.
It's probably not a big deal until you start to care about encapsulation and separation of various layers of your app. So this project aims to provide a tool for search and refactor such "unnecessary-public" types.

## How to use it

Currently this project has only a CLI interface. To analyze project run

`EncapsulationAnalyzer.CLI.exe analyze [path-to-sln-file] [project-name]`

It will display all found types in a table.

To automatically refactor found types, run

`EncapsulationAnalyzer.CLI.exe refactor [path-to-sln-file] [project-name]`

## Limitations and important details

## TODO-list

## Possible improvements

[ ] Package cli as a dotnet tool

[ ] Allow to pick which types to refactor

[ ] Wrap encapsulation analyzer into CodeRefactoringProvider so it can be integrated into VS and/or Rider