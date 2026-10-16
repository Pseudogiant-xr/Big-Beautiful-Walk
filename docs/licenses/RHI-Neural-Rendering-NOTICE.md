# Community Neural Rendering runtime

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
