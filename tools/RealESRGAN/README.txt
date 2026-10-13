Local comic super-resolution, 2026-10-11

Official upstream executable and models:
https://github.com/xinntao/Real-ESRGAN/releases/tag/v0.2.5.0
https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesrgan-ncnn-vulkan-20220424-windows.zip

Archive SHA-256:
abc02804e17982a3be33675e4d471e91ea374e65b70167abc09e31acb412802d

The portable executable uses ncnn Vulkan. It does not install a service,
modify machine settings, or require CUDA/PyTorch. The selected GPU is the
NVIDIA GeForce RTX 5080, device 0. Child processes run hidden.

Selected model: realesrgan-x4plus (official general image restoration model).
Processing: neural 4x output first, followed by Pillow 12.3.0 Lanczos
downsampling to 2x the input dimensions. No original Unity resource is edited
by this script. The 4x neural output, execution log, hashes and parameters
are preserved in D:/Game/Artifacts/ComicHD20261011/SuperResolution.
Final 2x PNGs are in D:/Game/Artifacts/ComicHD20261011/Enhanced.
Combined manifest: D:/Game/Artifacts/ComicHD20261011/super-resolution-manifest.json

Command pattern (all paths passed as separate subprocess arguments):
realesrgan-ncnn-vulkan.exe -i INPUT -o RAW_OUTPUT -n realesrgan-x4plus
  -m MODELS_DIRECTORY -s 4 -t 256 -g 0 -j 1:1:1 -f png

License notices are retained here:
- LICENSE-ncnn-MIT.txt: upstream ncnn executable project MIT notice
- LICENSE-RealESRGAN-BSD-3-Clause.txt: upstream Real-ESRGAN BSD-3-Clause notice

Source license URLs:
https://raw.githubusercontent.com/xinntao/Real-ESRGAN-ncnn-vulkan/master/LICENSE
https://raw.githubusercontent.com/xinntao/Real-ESRGAN/master/LICENSE

Super-resolution reconstructs detail inferred by the model. It is not a
claim that the input was originally authored at the larger resolution.
