import sys
from os import path
import os
import zipfile

sys.stdout.reconfigure(encoding='utf-8')

ROOT_PATH = os.getcwd()
FILE_PATH = path.join("RimworldExtractorGUI", "Program.cs")
PORTABLE_PATH = path.join("publish_standard", "RimworldExtractorGUI.exe")
TEMPLATE_PATH = path.join('.github', 'release_template.txt')


def write_output(key, value):
    if 'GITHUB_OUTPUT' in os.environ:
        with open(os.environ['GITHUB_OUTPUT'], 'a', encoding='utf-8') as state_file:
            state_file.write(f"{key}={value}\n")
        print(f'GITHUB_OUTPUT: written {key}={value}')
    else:
        print(f'{key}={value}')


def edit_template(changelog_path):
    """Antepone el changelog al texto fijo de la release.

    Recibe la ruta de un archivo y no el texto: el changelog tiene varias lineas y
    pasarlo como argumento obligaba a escaparlo para PowerShell, que se rompe con
    cualquier comilla en un mensaje de commit.
    """
    with open(changelog_path, 'r', encoding='utf-8') as file:
        changelog = file.read()
    with open(TEMPLATE_PATH, 'r', encoding='utf-8') as file:
        lines = file.readlines()
    # Sin el "##" que se agregaba aca: salia en cada release como un titulo vacio.
    lines.insert(0, changelog.strip() + '\n\n')
    with open(TEMPLATE_PATH, 'w', encoding='utf-8') as file:
        file.writelines(lines)


def write_current_version(version):
    if not path.exists(FILE_PATH):
        print(f'no file exists in {FILE_PATH}')
        return False
    try:
        lines = []
        with open(FILE_PATH, 'r', encoding='utf-8') as file:
            lines = file.readlines()
            for (idx, line) in enumerate(lines):
                if "string VERSION" in line:
                    start = line.index('"') + 1
                    # Con el salto de linea: sin el, la linea siguiente del archivo
                    # quedaba pegada al final de esta.
                    lines[idx] = f'{lines[idx][:start]}{version}";\n'
                    print(f'write_current_version done: {lines[idx]}')
                    break
        with open(FILE_PATH, 'w', encoding='utf-8') as file:
            file.writelines(lines)
    except ValueError as err:
        print("ValueError {0}".format(err))
        return False
    return True


def cleanup_standard():
    standard_path = "publish_standard"
    bin_path = path.join(standard_path, "bin")
    os.mkdir(bin_path)
    os.listdir(standard_path)
    for entry in os.listdir(standard_path):
        full_path = path.join(standard_path, entry)
        if path.isdir(full_path):
            continue
        ext = entry.split('.')[-1]
        if ext == 'dll' and entry != 'RimworldExtractorGUI.dll':
            os.rename(full_path, path.join(bin_path, entry))
    # Sobran en el zip, pero no siempre estan: publicando solo el proyecto de la interfaz
    # no aparece el deps.json de la libreria, y el .pdb depende de como se compilo.
    for sobrante in ("RimworldExtractorGUI.deps.json", "RimworldExtractorInternal.deps.json",
                     "RimworldExtractorInternal.pdb"):
        completa = path.join(standard_path, sobrante)
        if path.exists(completa):
            os.remove(completa)

    zip_filename = path.join(ROOT_PATH, 'RimworldExtractor-Standard.zip')
    with zipfile.ZipFile(zip_filename, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for root, _, files in os.walk(standard_path):
            for file in files:
                zipf.write(path.join(root, file), path.relpath(os.path.join(root, file), standard_path))
    print(zip_filename)


def cleanup_portable():
    portable_path = "publish_portable"
    print(path.exists(path.join(portable_path, 'RimworldExtractorGUI.exe')))
    new_path = path.join(ROOT_PATH, 'RimworldExtractor-Portable.exe')
    os.rename(path.join(portable_path, 'RimworldExtractorGUI.exe'), new_path)
    print(new_path)


if __name__ == "__main__":
    print(ROOT_PATH)
    command = sys.argv[1]
    if command == 'version':
        result = write_current_version(sys.argv[2])
        if result is True:
            quit(0)
        else:
            quit(1)
    elif command == 'cleanup':
        cleanup_standard()
        cleanup_portable()
    elif command == 'template':
        edit_template(sys.argv[2])
