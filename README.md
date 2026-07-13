# Theme Converter for VS26

Convert Visual Studio Code color themes into Visual Studio `.pkgdef` theme files.

## Install

```powershell
dotnet tool install --global ThemeConverter.VS26
```

## Usage

Convert a theme file, or every `.json` and `.jsonc` file directly inside a folder:

```powershell
theme-converter convert <theme-json-or-folder> [--output <folder>]
```

Install a converted theme into a Visual Studio installation:

```powershell
theme-converter install <pkgdef-or-folder> --target-vs <visual-studio-root>
```

Use `--help` to see all options. Installation may require an elevated terminal when Visual Studio is installed under `Program Files`.

## Development

```powershell
dotnet test ThemeConverter/ThemeConverterTests/ThemeConverterTests.csproj
dotnet pack ThemeConverter/ThemeConverter/ThemeConverter.csproj --configuration Release
```

## License

[MIT](LICENSE)
