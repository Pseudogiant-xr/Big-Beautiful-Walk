# Community Neural Rendering runtime

## NR 310.9.16 local import

Setup also accepts a user-supplied compatibility DLL with file version `310.9.16.0`, size 276,162,560 bytes and SHA-256 `CDFA6C1EC784867182164C7836F324F087B5C9F374D0C9FE46F54999FEABF3ED`. It matches the reference output documented by [OpenNR at 57ca170](https://github.com/clshortfuse/openNR/tree/57ca170cb836f549f0e64544f282d535666b0f7d). Windows reports `NotSigned`; the hash identifies the supported bytes and is not a vendor signature or approval. OpenNR's source licence does not relicense the vendor runtime or model assets. No model or generated DLL is shipped in this repository or ZIP.

This path copies the user's file after verifying it, includes the existing bridge and leaves saved settings unchanged. Fresh NR remains disabled. Rendering with this model in Big Walk is not yet verified. There is no automatic 310.9.16 download in this release.

## SF-v2 download

Setup's separate **Neural Rendering runtime** option downloads the SF-v2 model from [RankFTW's RHI runtime mirror](https://github.com/RankFTW/rhi-repo/releases/tag/dlssnr-310.8.SF-v2). This is a community-hosted variant, not an official NVIDIA SDK download. Credit goes to RankFTW / ReShade HDR Installer (RHI) for hosting and the version catalogue. This project does not incorporate any RHI implementation code.

The downloaded archive and model are not in Big Beautiful Walk's repository or release ZIP. The download is optional and hash-pinned, and it leaves NR disabled at first. The core install and the upscaler/FG option do not select it. Existing files and settings keep the installer's usual ownership and conflict protections.

| Item | Recorded identity |
| --- | --- |
| Archive | `nvngx_dlssnr_310.8.SF-v2.zip`, 116,693,212 bytes |
| Archive SHA-256 | `1DA35941894994EB087E017577829E492454E9BAE3A6A9397027069CEB74955C` |
| Sole member | `nvngx_dlssnr.dll`, 165,830,144 bytes |
| Model SHA-256 | `6EB209E764F39872625DEBD6ABAF45E2BB6322F6F270F781F70C059AE30B3927` |
| File/product version | `310.8.SF.0` |
| Windows signature check | `NotSigned` |
| Test correspondence | Byte-identical to the model previously tested in Big Walk; the installer download path is checked separately |

The archive contains no licence or notice file. NVIDIA and other rightsholders retain their rights. Neither this project's Apache licence nor RHI's GPL licence grants rights in this model. The mirror being available, and the hash matching, do not establish NVIDIA authorisation or a grant to redistribute the model. The project does not claim that this NR model is official, signed, the latest, or universally supported.

RHI's [pinned version catalogue](https://github.com/RankFTW/RHI/blob/b0570336431bfce265e0f6dc19156ea751e404f7/dlss_manifest.json) records the download endpoint. RHI's source is GPLv3. Using the download address it lists does not incorporate RHI's downloader implementation or relicense the model. OptiScaler's integration and forwarder stay under OptiScaler's own [GPLv3 licence](OptiScaler-LICENSE.txt).
