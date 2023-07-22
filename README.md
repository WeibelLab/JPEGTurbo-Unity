# JPEGTurbo-Unity
![Example of JPEGTurbo-Unity running on the editor](screenshot.png)

A libjpeg-turbo wrapper for Unity - It runs on HoloLens and Oculus Quest.


# Roadmap

- [x] Decoding JPEGs to RGB (~92ms of latency on a local 5G WiFi using HoloLens 2, ARM64)
- [ ] Decoding JPEGs to YUV (faster as YUV can be transformed to RGB in a shader
- [ ] Encoding JPEG

# Installing JPEGTurbo-Unity

There are two ways to install JPEGTurbo-Unity:
 
 1. Installing JPEGTurbo-Unity through Unity's Package Manager (**Recommended**).
 2. Downloading and importing the assets package

## Installing through the Unity Package Manager

> This is the recommended way of installing this package!

> While this repository remains `private`, we highly recommend using the following URL on Unity:
`https://a64476f1ebd7960aedac3357a4ed4bd74b097d51:x-oauth-basic@github.com/WeibelLab/JPEGTurbo-Unity.git`

The above URL uses a safe authentication route that won't require typping your username or password. Support for that URL will remain even after this repository goes public.

Using the URL mentioned above, follow instructions from https://docs.unity3d.com/Manual/upm-ui-giturl.html


## Downloading and importing the assets package
Please, head to the [Releases](https://github.com/WeibelLab/JPEGTurbo-Unity/releases) webpage to download the latest Unity package for JPEGTurbo-Unity.

# Projects using JPEGTurbo-Unity
If you wish to add your project here, please, [create an Issue](https://github.com/WeibelLab/JPEGTurbo-Unity/issues)

# License
<a rel="license" href="http://creativecommons.org/licenses/by-nc-sa/4.0/"><img alt="Creative Commons License" style="border-width:0" src="https://i.creativecommons.org/l/by-nc-sa/4.0/88x31.png" /></a><br />This work is licensed under a <a rel="license" href="http://creativecommons.org/licenses/by-nc-sa/4.0/">Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License</a>.

# Citation
[![DOI](https://zenodo.org/badge/278199660.svg)](https://zenodo.org/badge/latestdoi/278199660)

If you make use of this library in your research project, please, don't forget to refer to it by citing the following work:
```
@inproceedings{gasques2021artemis,
  title={ARTEMIS: A collaborative mixed-reality system for immersive surgical telementoring},
  author={Gasques, Danilo and Johnson, Janet G and Sharkey, Tommy and Feng, Yuanyuan and Wang, Ru and Xu, Zhuoqun Robin and Zavala, Enrique and Zhang, Yifei and Xie, Wanze and Zhang, Xinming and others},
  booktitle={Proceedings of the 2021 CHI Conference on Human Factors in Computing Systems},
  pages={1--14},
  year={2021}
}
```
