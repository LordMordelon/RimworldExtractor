"""Genera el logo de la aplicacion: logo.ico (el del ejecutable y las ventanas) y logo.png.

El dibujo es un planeta con anillo —el mundo del borde— que ademas es un globo de
dialogo, por la traduccion. Se define aca, con geometria y colores, y no en un archivo de
imagen: asi cambiar un color o un tamaño es cambiar un numero y volver a correrlo.

Uso, desde la raiz del repositorio (necesita Pillow):

    python RimworldExtractorGUI/Resources/generar_logo.py

Cada tamaño del .ico se dibuja por separado y no achicando el grande: por debajo de
48 px se sacan las estrellas, que ahi solo serian ruido, y el anillo nunca baja de
~1,6 px para que no desaparezca en la barra de titulo.
"""
import math
import struct
import sys
from io import BytesIO
from pathlib import Path

from PIL import Image, ImageDraw

BASE = 256  # la geometria se define sobre un lienzo de 256 y se escala

FONDO_ARRIBA = (46, 62, 74)
FONDO_ABAJO = (24, 33, 41)
BORDE = (64, 82, 96)
ANILLO = (246, 231, 197)
ANILLO_ATRAS = (196, 178, 146)
PLANETA_LUZ = (247, 186, 98)
PLANETA_MEDIO = (226, 122, 56)
PLANETA_SOMBRA = (160, 66, 40)
COLA = (205, 92, 46)
ESTRELLA = (226, 232, 238)

CENTRO = (128, 120)
RADIO = 64
ANILLO_RX, ANILLO_RY, ANILLO_ANGULO = 108, 30, 18

# Los tamaños que Windows pide segun la escala de pantalla: 16 a 48 para la barra de
# titulo y de tareas, 256 para el explorador en vista grande.
TAMANOS_ICO = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def mezclar(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def punto_elipse(t, esc, crece=0.0):
    """Un punto de la elipse del anillo, agrandada o achicada en `crece` (en unidades de 256)."""
    th = math.radians(ANILLO_ANGULO)
    x = (ANILLO_RX + crece) * math.cos(t)
    y = (ANILLO_RY + crece) * math.sin(t)
    return ((CENTRO[0] + x * math.cos(th) - y * math.sin(th)) * esc,
            (CENTRO[1] + x * math.sin(th) + y * math.cos(th)) * esc)


def arco(draw, desde, hasta, color, ancho, esc, pasos=360):
    """Una banda entre dos elipses concentricas. Un trazo grueso de Pillow sale dentado."""
    medio = ancho / esc / 2
    afuera = [punto_elipse(desde + (hasta - desde) * i / pasos, esc, medio) for i in range(pasos + 1)]
    adentro = [punto_elipse(hasta - (hasta - desde) * i / pasos, esc, -medio) for i in range(pasos + 1)]
    draw.polygon(afuera + adentro, fill=color)


def bezier(p0, p1, p2, pasos=40):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t ** 2 * p2[0],
             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t ** 2 * p2[1])
            for t in (i / pasos for i in range(pasos + 1))]


def dibujar(tam):
    """El logo en un cuadrado de `tam` px, con fondo transparente fuera de la placa."""
    sup = 8  # supermuestreo: se dibuja grande y se achica, para bordes suaves
    lado = tam * sup
    esc = lado / BASE
    img = Image.new("RGBA", (lado, lado), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Placa de fondo con degradado vertical.
    m = 8 * esc
    radio_placa = 54 * esc
    mascara = Image.new("L", (lado, lado), 0)
    ImageDraw.Draw(mascara).rounded_rectangle([m, m, lado - m, lado - m], radio_placa, fill=255)
    degradado = Image.new("RGBA", (lado, lado))
    dg = ImageDraw.Draw(degradado)
    for y in range(lado):
        dg.line([(0, y), (lado, y)], fill=mezclar(FONDO_ARRIBA, FONDO_ABAJO, y / lado) + (255,))
    img.paste(degradado, (0, 0), mascara)
    if tam >= 32:
        d.rounded_rectangle([m, m, lado - m, lado - m], radio_placa, outline=BORDE, width=round(3 * esc))

    if tam >= 48:
        for (x, y, r) in ((52, 58, 3.2), (196, 44, 2.6), (222, 206, 2.8), (168, 214, 2.0)):
            d.ellipse([(x - r) * esc, (y - r) * esc, (x + r) * esc, (y + r) * esc], fill=ESTRELLA)

    ancho_anillo = max(12 * esc, 1.6 * sup)

    # Mitad trasera del anillo: queda detras del planeta.
    arco(d, math.pi, 2 * math.pi, ANILLO_ATRAS, ancho_anillo, esc)

    # Cola del globo de dialogo, abajo a la izquierda.
    c = (CENTRO[0] * esc, CENTRO[1] * esc)
    r = RADIO * esc
    a1, a2 = math.radians(118), math.radians(152)
    p_a = (c[0] + r * math.cos(a1), c[1] + r * math.sin(a1))
    p_b = (c[0] + r * math.cos(a2), c[1] + r * math.sin(a2))
    punta = (62 * esc, 196 * esc)
    cola = bezier(p_a, (84 * esc, 188 * esc), punta) + bezier(punta, (70 * esc, 170 * esc), p_b) + [c]
    d.polygon(cola, fill=COLA)

    # Planeta con degradado radial: circulos concentricos que se corren hacia la luz.
    luz = (c[0] - 0.38 * r, c[1] - 0.40 * r)
    pasos = 90
    for i in range(pasos):
        t = i / (pasos - 1)
        radio = r * (1 - t) + (0.18 * r) * t
        cx = c[0] + (luz[0] - c[0]) * t
        cy = c[1] + (luz[1] - c[1]) * t
        color = mezclar(PLANETA_SOMBRA, PLANETA_MEDIO, t * 2) if t < 0.5 else \
            mezclar(PLANETA_MEDIO, PLANETA_LUZ, (t - 0.5) * 2)
        d.ellipse([cx - radio, cy - radio, cx + radio, cy + radio], fill=color)

    # Mitad delantera del anillo, con un filo oscuro que la separa del planeta.
    arco(d, 0, math.pi, PLANETA_SOMBRA, ancho_anillo + 4 * esc, esc)
    arco(d, 0, math.pi, ANILLO, ancho_anillo, esc)

    return img.resize((tam, tam), Image.LANCZOS)


def entrada_bmp(img):
    """Un tamaño del .ico como DIB de 32 bits: lo que leen todas las versiones de Windows."""
    w, h = img.size
    cabecera = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, 0, 0, 0, 0, 0)
    filas = []
    for y in range(h - 1, -1, -1):  # los DIB van de abajo hacia arriba
        fila = bytearray()
        for x in range(w):
            rr, gg, bb, aa = img.getpixel((x, y))
            fila += bytes((bb, gg, rr, aa))
        filas.append(bytes(fila))
    # La mascara AND va igual, aunque con alfa no se use: una fila de bits por pixel,
    # rellena a 32 bits.
    mascara = bytes(((w + 31) // 32) * 4 * h)
    return cabecera + b"".join(filas) + mascara


def escribir_ico(ruta, imagenes):
    """Hasta 64 px en BMP y los grandes en PNG, que es como los guarda el propio Windows."""
    datos = []
    for img in imagenes:
        if img.width >= 128:
            buf = BytesIO()
            img.save(buf, "PNG", optimize=True)
            datos.append(buf.getvalue())
        else:
            datos.append(entrada_bmp(img))

    salida = struct.pack("<HHH", 0, 1, len(imagenes))
    desplazamiento = 6 + 16 * len(imagenes)
    for img, dato in zip(imagenes, datos):
        lado = img.width if img.width < 256 else 0  # 0 significa 256
        salida += struct.pack("<BBBBHHII", lado, lado, 0, 0, 1, 32, len(dato), desplazamiento)
        desplazamiento += len(dato)
    Path(ruta).write_bytes(salida + b"".join(datos))


def main():
    carpeta = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).parent
    escribir_ico(carpeta / "logo.ico", [dibujar(t) for t in TAMANOS_ICO])
    dibujar(512).save(carpeta / "logo.png", optimize=True)
    print(f"Listo: {carpeta / 'logo.ico'} y {carpeta / 'logo.png'}")


if __name__ == "__main__":
    main()
