# Community Neural Rendering runtime

Setup's separate **Neural Rendering runtime** option downloads the SF-v2 model from [RankFTW's RHI runtime mirror](https://github.com/RankFTW/rhi-repo/releases/tag/dlssnr-310.8.SF-v2). This is a community-hosted variant, not an official NVIDIA SDK download. Credit goes to RankFTW / ReShade HDR Installer (RHI) for hosting and the version catalogue. No RHI implementation code is incorporated in this project.

The downloaded archive and model are excluded from Big Beautiful Walk's repository and release ZIP. The download is optional, hash-pinned and leaves NR disabled initially. Core installation and the upscaler/FG option do not select it. Existing files and settings retain the installer's ordinary ownership/conflict protections.

| Item | Recorded identity |
| --- | --- |
| Archive | `nvngx_dlssnr_310.8.SF-v2.zip`, 116,693,212 bytes |
| Archive SHA-256 | `1DA35941894994EB087E017577829E492454E9BAE3A6A9397027069CEB74955C` |
| Sole member | `nvngx_dlssnr.dll`, 165,830,144 bytes |
| Model SHA-256 | `6EB209E764F39872625DEBD6ABAF45E2BB6322F6F270F781F70C059AE30B3927` |
| File/product version | `310.8.SF.0` |
| Windows signature check | `NotSigned` |
| Test correspondence | Byte-identical to the user's previously tested Big Walk model; the installer download path is checked separately |

The archive contains no licence or notice file. NVIDIA and other rightsholders retain their rights; neither this project's Apache licence nor RHI's GPL licence grants rights in this model. The mirror's availability and a matching hash do not establish NVIDIA authorization or a model redistribution grant. This notice records that limit explicitly; the project does not claim an official, signed, latest or universally supported NR model.

The endpoint is recorded in RHI's [pinned version catalogue](https://github.com/RankFTW/RHI/blob/b0570336431bfce265e0f6dc19156ea751e404f7/dlss_manifest.json). RHI source is GPLv3; using its factual download endpoint does not incorporate its downloader implementation or relicense the model. OptiScaler's integration/forwarder remains under its own [GPLv3 licence](OptiScaler-LICENSE.txt).
