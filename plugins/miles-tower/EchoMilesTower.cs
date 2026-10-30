// EchoMilesTower 1.0.0 (Nick, 31 Oct 2026) - Miles' Lookout: a memorial lookout tower for MILES.
// Built in-game from stone building pieces, so it works on every map with no map edits. Each wipe it finds the
// highest clear, flat spot away from monuments, roads, rivers and bases, and builds the tower there:
//   2x2 squares, 6 storeys (about 18 m), spiral stairs inside, windows on the way up, an open top with low railings
//   and no roof, a fire beacon and lanterns that never go out, a plaque at the door and one at the top, a gold map circle.
// Nothing at the tower can be damaged, looted, picked up, upgraded or demolished, no one can build within 30 m,
// and there is no PvP within 25 m. Admin console: milestower.status | build | remove | flipsigns
// In game (admin): /milestower here - rebuild it where you stand.
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EchoMilesTower", "Echo 10X", "1.0.1")]
    [Description("Miles' Lookout - a memorial lookout tower for MILES, rebuilt on every map.")]
    public class EchoMilesTower : RustPlugin
    {
        private const ulong Marker = 9900099UL;
        private const int Levels = 6;
        private const float Sq = 3f, LevelH = 3f, PeaceRadius = 25f, NoBuildRadius = 30f;
        private const string PFound = "assets/prefabs/building core/foundation/foundation.prefab";
        private const string PFloor = "assets/prefabs/building core/floor/floor.prefab";
        private const string PWall = "assets/prefabs/building core/wall/wall.prefab";
        private const string PWindow = "assets/prefabs/building core/wall.window/wall.window.prefab";
        private const string PDoorway = "assets/prefabs/building core/wall.doorway/wall.doorway.prefab";
        private const string PLow = "assets/prefabs/building core/wall.low/wall.low.prefab";
        private const string PStairs = "assets/prefabs/building core/stairs.spiral/block.stair.spiral.prefab";
        private const string PFire = "assets/prefabs/deployable/campfire/campfire.prefab";
        private const string PLantern = "assets/prefabs/deployable/lantern/lantern.deployed.prefab";
        private const string PSign = "assets/prefabs/deployable/signs/sign.large.wood.prefab";
        private const string PMarker = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        private const string PlaqueB64 = "iVBORw0KGgoAAAANSUhEUgAAAgAAAAEACAIAAABK8lkwAABbdklEQVR42u2dd1wUxxfA3/Xjjt57BxEQxAIWFBUb9l6iRhO7xkSNiYkmGk0xiab7S1Wjib33jl0ELKig0nvvHO36/f5YWJZrHAhG4H0/98fe7NvZmTezb/oMzc3BAhAEQZDOBxMARCIJKgJBEKSzQUfrjyAI0kkLAFQBgiAIdNouIATptHC5bFQC0nkLAPwAEARBsAWAIJ0RoVCMSkA6bwGAHwCCIEhnLAA0Wf9j2/qhdhAEQToAk9dEAM4CQhAEQbAAQBAE6fQFAINBQy0gCIJA5xwEbrIMmLb2HmoK6ajIZApUAtLx0GUcF6eBIp0dbAQjnTaTM3Wp/mAVCUHrjyCAC8EQBLD/B0E6RD5vdgEwO8wBtYkgCPIasvdCVvNaALo0gZVkDlzORkUjCIK8Vswcbq/Jnmtyx3UACIIggAvBEARBkE5ES2YB4bgZgiAItJ/BXk3u2AJAEATBaaAI0vng8/VQCUiHz8+a3Jm6fABKMvjNIAiCYAsAQdo91dW1qASkY2dmTe5MXT4AJQH8YBAEQToAdLTmCIIguA4AQRAEwQIAQRAE6fAFgLGxIWoBQRAEOucsoCbLACUBLDOQjkR5uQCVgOA0UATpjGCFBunwmVmTO1OX6k9jGSOsMSFo/REEWwAIAtj/gyCvBxpr55rcsQBAOjUsFgOVgHT4/KzJnanLB6Akg98MgiAIYBcQgrR3JBIZKgHp2JlZkztTlw9ASQA/GARBkA4AHa05giAIbgWBIAiCAI4BIEjbYsBnf7o8sE9367zC6m/+evjoWSHqBEFedQHAYNA62yHv86f6LJ3ZTYvA+KVncwqqmvQn0M/q188Ga5cZOOtoTa0UAGaN67JqXkCTfioU0H/GYbFEDgCzx3mtnNe9yUc2/hx57ka6FoF3ZvvPm9T1ZTRWUFwzetFptbe6eZoN7uPg72XuYK1voM+WyRSlFcKi0tqHzwojH+fHPC9UaMhcC6f7DOljDwBujkZbP+wftuAUEWtdaC3NaMLF3jDQz7p3N0tbS76xIcfYkCOTKcorReUCUXpOZfTT/KgnBYUlNf+Jzwun+Sye0a3JF3249e61e1nk3/A9E40MOFrkv/3r4eELSa0eYDqdFnl4Gp1OazLAP//z+J+T8VSX07+PtbXka3lk0OxjE4e5vTe3e2tZhp/2PP73VHynawEwGE0kj5JAk/KvOWbGXO0Co0Kcdh173qQ/g4LstQuUVghFYhmhLitzni5hKyqtkckVxCPWFjo9kldUrT1FaC+dXGwWXfUVHs7GHy7o6dfFnOrIYoKtJd/Wku/vZf72ZO+UzIodR55di8xS9bOrqyl5bWTAcbAxSM/RdU1Wa2lGld7drJbO7ObjYaZ8gwV6XKaNBb+rm2nYQCcAuBGd/fuB2LRswSv22diQo8vr8osbxf3xi+J+PWxYTLraOkeZQJiSVaGkq1YJsKUZTxfrDwCZeVXUANBoYG6i7TvNKaiqFUlp9Na0RTQ6rb0bN03h1+TeGbuADp1PBIDxoa4ctvoFDSMG6FQADOhlq+mWTKY4fCHx4LnEhupMeKqgUhzc09bX00yTtTpwNvHuw1zS5cC5xBqhdPJwdyMDtqp8VY3k4LnE8HtZqVkV2sP56/6n52+mvzPbb0AvO7Ufv+ojLCbDgM9qyCVMZUXNndh1yYxu1G87u6AqLUsgFEktzXjdPM2IW26ORlve73f8csp3ux5JZY0q+EKxtPHfZkxGaC3NUOFyGJve7TMoULlQT0gry8qrYrPoro5G9lb6DcV/oP3A3nZ7Trz4/UDsq/T5l3+fpGULlszsZqyuRi8Sy05cSQm/l5WUVt64QXCHp8f8ZGlgaF8HqvDOo89OX0srqxC2UYALS2rGLTnTp7v1+FBXNWUJQJlAtOfEiyt3M4vLapVy5qiFpwP9rMYNce3T3Zp6Kyuv8o9DcTejs9VWucoFonKBqLxSJBLLBgXa63EbmbhaofRGdDabxeDpMS1NeVbmPH0eq1N3AenS/6Mk0967jDJyKr/96+HBs4k7vgw1VdcacLYz9HQ2eZFSqsUTT2djGwuN7dPlm64/iGvUqZ2cUZGcUbHjyLPhwY5fre6nJP88uXTxhmu1wkY2MSe/6td9T/858eKfrcMdbQyUHnnj/Yu5BdU6Rjkls2LVV7cP/xTm6mDUyOyKpMPfOgnqOuiPbR9lasQlWwDURJ89zmvZG37k33KB6Ks/HlA7HKzMeeuX9O7Xw4b4O2m4GwB89fv9RlXUooZ+A7lCUVhco3u+akXNkNXqH9cNVCqbkzPKN/wUmZheTtZJB/Sy+2xFkKF+XalDp9HemuRtYaL3+a/RmgLf6j7LZLKjF5NvP8j959vhSm1ZqUw+54NLqVnqmw6VVZLnyaVkASCWyFZsvvHoeVFbBzi3sPr45ZQTV1L+t3FwoJ+Vco/W+nBNLb9ygejynczLdzI/Xtxr8gh30rfpKy+K6+cuKuQKank298MreUUNSX/mj7FKBUB5pejTHyOpGfXkr2PIhpFCrmjvxk1T+DW5d95ZQJl5lcXlQk13ibatFgb2ttNy93myxsIjLrFETZmUW6lk/an12bIKkap7c20cAEh1ztyV1eLte59SGgQN+aSnr6VS//u67yOo1h8ACopr1n0fIabMMJ44zM3VodG2a1n5VVR5iVTe3Oi0lmZoNPhmTX8lk5edXzV/fThp8og66a37OUs3XldqyowZ7EItDl+NzwXFNSUquVcikWuy/gDg5Woyf6oP+cZPf4xUtf5tF2CFAqqqxWpzWpMJVCuSUqssYg0z18/dSKdaf9BtcEsp6+IsIAQAYHiw4497HsvlGi1mSKBdx9bA+Rvp787xJ3qc6XQanU4jtKE0AllcVhv9tECtdS6vFFua6pGWpZevFdU8pVO6jHXv/W91uFz2jFEePX0tldy/3/1EJqdxucpdTBl51UcupMwc40F1nDPBK/JJYaxK0d52PhOVblXzreotgY0F7+dPQnj11eHfDsTdjSlQFW7bADPUVDc5XDaXK29ypgoljo1CwqRsS3P1XrZSIGlqVKQckVsP8kYMcCJ906TA9oKm8GtyZ+oSYSWZ9q4jLZ8QibmJXv+edvdjCzXc5XpRxjDVZmu5htYVh8NSN0RD16JVtcNoLUiFZpkMALgbkz86pO7bMNDnisQyY0NOD28LqkyNUKrWBwtTPQsTPaqLXNHo28stqqU0gKpalqleXjOG+uyF07yVHLMLqtSWagQnw1OVrB6dRls11//t9ddejc/NxVCfvW1tf7K38/S1tP1nE1+lKl4BQpH0eXJZCx6MSyrFFgCirhHQ30FTARDc04a0pUKRjMvpmLvjnbuRrsdhsFiM7Lwqovrvaq+8e76lKY/FpKt24CyZ4UMtbuQKRVxSiVLXsEQiZ7HoAJCWXflfxXFUiJPqXICrd7OEQo1dEykZ4sT0ck9nY6qjp4uxu6MBtX9vynDXNvKZVKlqN4uq52wWY8vqPk62dSMl9x7nffmb+hGLNg+wTE1NXyQUa/Ef6rouZZQ4Kqjy0vruoIzcyqpqoYpCVFWkUHpdTr64sLSWaKpKJbImA/Oaoyn8mtyZWu5peri960jtJyRXKJQqyCG97b74NVqkbnZKv4CGaQkRj3KHUGZWULK1+j59kYilbohGrkWranuiWpAKOpoMkugnedFP8pT8UJ3iMqCn9fmb6UruN6Oyu3maEfO45QrF7wdi41OULUJ6rsDDyRgAElJLWpapXl4z4wY7q6sVlmh/6llSiZLVA4BJw92pVo8Y+m4Ln5vV5vtiZR9/r7qpukkZ5Wu33tU0Hvg6BLi53LyfU10rZdBpqdkVLfZkw4/3XOyNZHLFg7iCTtcC6DDW/CWJepLft7sN1YWnxwzpbXf5bqaSJI/L7OVrRc6OoA5mdnjKBWoGXT9Y0CMxvTw5o9G8w8t3M69GZHV1NzU34SZnVKhdWJecUUEUAM2ar9mK2FryHW0NVN3jU5roFkhIU9PbQJ2t2HY+N4tVbwWQtZPistqVX94iliW+SlW0KZm5lZm5L9t8fBBXqDRnD3AvoM7G2evpqo4j1c0F6htgw2bV6e16VE7HU8XoQc4Pjs94cHyGp4uJ0q28wmrVlb0GfPbfW4aOHeKi2uB4llRyMzpH07LqvafiN22PWv/DPU1Wqa1ROzNdrlAUltZCU7NHVB0tTfXM64c92s5n3Zk9zmvmGE+on/++8stbaj1/fQKM4BjAf0ZatuB5cqm3e6Oh3X49bIwMOBWVIk3zf65HZpFTCDoM44e6abpVJhA9Ty5RNRZ6XObGd4KmjvT4bX/svcd5Or4oIa1Mbf3xlaGU3ASl5SIGg87QOqxTXqm+3eznZX77QS4AqF3u1yo+g5YF3rSGw5pC+zqQeyTI5YpPf4pMyRJoOcrpVQRY3Yg9i8Vo8oApOp1OjbbuB1LRgKbq0rHPs8ITwaBZE8Api13pF25nKFkEJoM+cqDT8csp1GknwT1t6xejV6flVKqdiMJkMVgaelpVV9US3mrRqtrJSi1IBe0mA+qX+JLzfJhMuupbjlxMVltbJOzpLxtCEtPL956KD4/M1jKJti1SsAWasaMsZ6WuFWrywTKBehnr+g0/1C4SbBWfdaF7V4uNywNJ5ew69vzuoyZK5f82wAi2AP57rtzNfHeOv9J+KSMHNCoA/L3MydWPN++3+/4fJp0+LczD2JBjYsgxMmC72hs52Rlof+TSnczBQfZalkF4Ohtvfq/P4hlVO48+v3g7Q/Ear6xUuw2ASCRt8pCMmhqJWncel0k8y9djtpHPQBnAV0YBEonMxd7wmzX9WKyGbNzDx7LJ/SpeRYDVVQgkElmTr5DL5dRo636EiQIUqi4d+wQUPBGsGVA/IalUXlxae+dh7uDGW7x18zSzMOWSi0v7BzQMFF+NyJRIZGrruVLN2VoqlamdzaJFq2ptaAtSQdUfFou+SvO2mlKpXO1bPtp2d9O7fYYHO2qvXG9YHjhpmNuGnyNffpiujTSj1urpsimppsWo+vUbKKktWlrFZ+2Ym+j9/GkIWUepKwC8LcYMdjl7Pa25ZeErCDACeCIYvE4z31UdR1J6+ckdIErKhXEJJZ1TSxKpfP0PEd/+9bBKQ+0PKD3L+7aNeG1XTSttFEPuqANNb6qhXkaPw2xrn7XDZtPVbnb77pv+SqXCK1MFgrOA2g13HuSqznQMG+hMXLg6GDrY1PUa34jKliva/TkKtULpyPmnZr1/6d3Pb27aHrX/TIKOe/IoFHD4QtKEZWf3no4XimTaLcu3HwQrTbF9TVA7+0jtnsmqu6Wq97B+8Ufb+ayd3ILqv9VtZGtqxF0+y+8/UQWCBUC7QSqTX7qTASpnYni5mgBACGWD3GuR2R0jysVltQlpZRExeWeupX3/d8yGnyKhOcsCftz9ePSi078diC3VvLMeg0Hb9G4Q//Xbd1dtC4at07QIuqZN9Nra5ybZc+JFtrq1KROHu6md9fSfBxj5LwsAHo+LWqByTt2CAKIRQPb/VFaLH3bQlSNX7mY2dw1nRaVo55FnYxaf+W7XI0GV+s/e1Jg7PtT1dYusWiPFZjVdK+JosIykGW07n6HpTnn5tzseql0V/PHiXpr2v/oPA4zAfzsLqMkyQEmgw5QZ1Bmcelw2Ea/0vJq07EoXewOlFWHHrqT5eNRVoCJiCtgcNrturqSavk49Hhfo6pvAXD2Ouv0OtZXEameatiAVVP2h0Wiq/py7mUnMCid1oiOnrmXeeljw6dKevVR2lASA0L6OJ8Mz2i4FW6CZwlKRugPj9Jr0wcZS/Vyp4nIx8Wzb+axLaj6OL7v9MG9AT+Vut65upm+M63r8SuqrVEVDQ1BdZ5GeHpcnbrKXiUmJYzPqrGp3A+3YVV5NsdPkjl1AoHamo+opku/M6kbWnm7dz+3IbaCbGQNnnxw4+2RSRrN3aCirEH2w9d69x/mqt7q4GL9uMY1PVbMMzciQ3eS5gGYaTitMSC1va5915Jd/Y9UesrZwalcTI86rVEXrIpPL0UC1ZgugpkbYpJCSjC6PtAuoMzhrhWIyXqeuJi+a7q3UWB7Sx47c/vNGVCa5SZxUqqamX1sj1DQIJqxlqB170KJVtTNNW5AKqv4oFIpm+WNiyOnT3drD2cTT2dhQn/3mh5dBw+5ap39XPoyJy2FIJRJxq846e0nNPHqWr7arxIBH136qjIkBS90xvDU5+RVt7bOOqZmaKdx15NkylYFfPo+1ZLr3pz/ee2WqoIwzS9QtNRA1mV7UcwQqBCLd01ftbqAdxnzpYqubdMepWqDuZPba6CcFmja0iojJE4lf97mzv3422NneUC5XHDqX+O+p+Bb7s/vrYbZWfIlEvufEi8MXkvoG2Gx+rw95yBx5Sozq6tDbD3OH93dUmjiky7TCV6yZ1CyB0lFlAODlYqLd6nVR2SUJACIoq20LimvayGfd+fdU/JjBLqpbvIUNdDp1NUVp+7NXEGC1Owny9Jo2QdTjqSt0WJyMAM4CgpfdG07jqpkbUa/7/B9TY26gn5WlqZ61OU/tocc6Ym+t7+tpZmrEtTLnEUeDKc3tsbfW1/Qs9cAvcl+BV7A5RHM1c/xysqqYr4a9LqBh6zQ102mUvGo7n0Hn5RpqR4MBYO2iXkyV87naOsBqt43TcrA2iQvlIOvkzAq0Tq06ioZFgDquR2WrnRktlcmVtrh6DelLabtUv8RkjH6UZc9qJ3V4qav9kfsIgQ6HIf/nmjl3I131NObQfg5aPHG0MXB3MlbdFl+pG73tfNadyMf54erOvHWxN5wzwQtUVkG2aYCjn+ar3bZIexSMDTlulALg8YsitE6t3AKg09X8GpcSjdzVyrfHH61Rd2ejWxKp7Oq9TFV9PYgtqK4VN/KEprZc1fxSuvpNzbQ8oqHo1vgLpRxQUyuSqo0vuRmcrv4IpaqR9fMy1/RsV1cTlZZTVqun4MtrprpW/L99T0FlH4t+PTSuXJs8wh1UuuO/3fEIVCZWtpHPzeK7XTG16oaj5k/xUap9t3WAE9PLVTcFGTvYRftys3FDXMiB6MLS2pjnWABgFxC8mm0h0tT1/7zuG8BZm/PI/UoBoLq2hS0AZzvDHj6WlIWdEu1NBGi0H5xJUOMRlIzcygu3Ml5PzRy5mHg/VvkoqLULexrqs1SLEG93k+mjPUBl7dWLlBJV4bbzWffivLisZufRZ6qyXA7jgwU9XmWA6XTYvu+JkryVOW/VvO6a5J3tDeZP9SGF952Ol8nlbVc/aO8/1cp607GWy0Htr3Gp3shd0yPt66dQNFroyOUwlQQePStSGv6SKxTXorKVxNSulmSzmZrey+ey1G6coiWooH4CjPrfgmndqDPEq2ukDVEGNfslavJn0fRu1Pp+VbWUUFqjHgBbgwlD3ZRU5+tp/v3HA6hzqIQi2aZfosQSeasn4strhhidXvd9hFIPlZ2V/l9fDPV0NqG20gb2tvvfxsFKvednrqf9eThW04YZbeQzjQZslfN76TSa2ty4/0yC6pAMAAzsbffWJO9XE2By/Ozk1RQlx2mjPL9c1U9pa27iFb9vGsLXq/teHsYVHr6QqHt5r8dhqi5S47AYuqxu6zzQ7C3Vz84+tq1fQ0NvTQR5PTvMYe+FrPYebWc7wxVz/KmblAmqxLtPvDh3Pa2EsqXB4hndFk7zofY/LlgfDpSTsKaGuS+Z0U11SU5eUfWuo88v382kdsHbW+sP6eswf7K32k0RIh7lHb2UfD+2gNpgd7E3nDjMbcYYT9UFnJq2XjAx4lJl3/38ZkRMnruT8eQRblNHeqhaqLIKoabxUurfBevDH78o6uFt8d3HAwz4bKXBxsS0soLiGqFY5upg5NW480dQJf5o293op6182mpraYZaI978Xt8hfexB5dSajNxKNpPu7mSsNOgtlyv+Pvb8t6Z2Wm51n1VzL0l6jmDnkWfXo7KVNmjq3c3qt02D1foWm1hy9GLSvZj80vqc0HaqIEqp9+cHTAvzVO07Tc0SpOcIamolhvrsrm6mFqYNZ4rFPC9a880dHacAmRlzg3vZLprma6XuTILM3Mr9ZxNu3c8tLKnpYNZcyThrsuGdvQCYM96LPCxJ1SD+dTjuz0NxpMk++esY8u4Pu2P2nU4gbx3fPlrtSlRoOOpdsfHnyIu3M7S/FBqv4w+ZdZTYkW3qSI+1i3q+ZGTnr7vq39Xi3Tn+L+nPG6svJqaXAwCPy5wwzG3MYBfVo8BVu4OvRWZ/t+tRUVPHCjaX1tLMk/hiJcfe3ayWz/JTezwWqEwT+G3/09QsgY6vay2fdclIYol85Zc3lQrdf74drmUvIAB44/1LiZQD2tpOFQAQ4G2x9A2/gK4WtCbWmUFphfDg2cTdJ17oOIXM19Ns99fDdJFc9En4o441otCCAoDZ5Eo/AFCS0eWR1xlbK76WE6ZKK4RkBPOKqp/EF/t7mRN/b0bnkLdsLHjarT+hqDKBiHhEy0uh0VS5arlCQTxibdkKZyoJxTIbi9bxhwiVSCI7dD7x0PlEawt+3+7Wns4mbo5GVuY8fR6Lx2VW10oFVaLUrIqnCSWX72bmF1W3RYZpLc2oBuzR88L566862xkG+Vv38rW0teQbG3IM9TlyuaK8UlQhEKXnCqKfFkQ/LSDqj7pHrbV81iUjsVl0ajaG+jFYLQWAQgG5BVXUR9pOFQDwNKF46cZrtpb8Ab3suriYuDsZmRpx+XosLochFMkqa8RFJbXPkkuexBfffpAjlshpNF39t9Y5t2c3jm/HQFOMNLnTnGzUz+Q7/E3fhk66tQ3rBmcOtz9wORsQBEGQ1wkl46zJhiuvBJbJmmhbKQk0KY8gCIK8ejQZZ03udLTmCIIggOsAEARBECwAEARBkI5eAOjr81ALCIIg0DlPBGuyDFASwDID6UhUVdWgEpDOWwAgSGcGKzRIh8/MmtyZulR/GsuYYY0JQeuPINgCQBDA/h8EeT3QWDvX5I6zgBAEQQBPBEMQBEGgc3UBNVkGqJ4tgCAdBrkcdYDgGACCdNJWMOoA6eCZWZM7U5fqj+oBYQiC1h9BsAWAIID9PwjyOudnTe5Y/0EQBOmsLWA+Xw+1gCAIAp2zC6jJMkBJAMsMBEGQ1xBNxlmTO3YBIQiCdNYWQHV1bZNCSjK6PNIJObatHyoBQV4Nk9dEoBKatNVNumMLAEEQBKeBIlglQRCkUxUAuiyEwa0gEARBoMOtBEZbjiAI0om7gJpcDIlbQSAIgkDHWwmM1hxBEKRzgl1ACIIgWAAgCIIgeCIYgiAIAngiGE4DRQB3hEYQXAiGIHgmDILgiWAIgtYfQbAFgCCA/T8IgieCIQiCIO2lBWBkZNCkkJKMLo8gCIIgrxhNxlmTO7YAEARBOvEYQEVFpXahxgLGTcojCIIgrxyNxlmTOx2tOYIgCOBWEAiCIAgWAAiCIEhHLwDYbFwK0Nnhcjn+fj7DQgd2gLhYWpqHDOwb0N233Sl21buL7t48ffXi4Y6REAR8Hq9nD78Rwwe1u7SDzjMI3GQZoCSAZUZzcXZ2PLz/T1X3oSOnCgSVa1YvmzZlnNKt8xeufvb5tjYNlbu7y6L5c1xdnOztbel0WkWF4Obt9nqs8dvzZvbt08vV1clAXx8ALl669ux5/H8VmBYo1tLSfO6b0wCAz+etem9R+00IUgOLF77p7uZsa2NNo9EqKgTXb9xpF2nX3tFknDW5YxfQq6CgoPD3P/eIRGLS5dnzhA8+2iwQVALAmbOXzpy7rFAoGqz/xfA/dvzb5q0/Gp1Opzs62tHptPauYZFIbGCgT1iQ/75Z3YEU22IN0IBmZ2tDo9HaV9p1uhaAWCxtUkhJRpdHkMYaq/r9z38dHexHhYUSLpu/+D4hMYW4jo1LiI37tru/r4O9LQCIxZJ1n2x5BaGKe5bw7qpPzp3ea2dr3d5T9u89h8Kv3zl9fA/ULXxX/IdxaYFis7Pz/95zcNqUcbW1wh9+/qu9f2LN0sBrlXbt39RIm+WOnTmdnaKiEvIrbd/NrPyidq3Yn37Z8dMvOzpn1nrd0g5nASEIgiAdHCaTSZdKcUfE1x1PT7dZMyb27OlvYWEGCkVxSVnM49h/9h5NrO9HUkv/fr2nTBrj6+NlYmIkkUizc/Ii7t0/cOhkfn6hWnlrK4t5c6cPCO5jaWFeUSG4eu3WL9t3VdfUUGV69fSfOnmsv5+3qamJTCYrKCyKvv943/5jGZnZY0YN+2LzWiU/v/52+8HDJwFgwriRn21YAwDzF61++OgpAHy6btXkSaNJybcXrnoUE6t7ZEMG9p02ZZyPtyePx8vLL7hzJ0oXTerpcd1cnd3dnD08XH28u2RmZm/YtBUArCwt3NycPNxdu3i6+fp4bfn2l3uRD1qsyWYplk6nuzg7urk5eXq4enl52NvZTJj8Vgte3Sr615K+hJixsdGokUMcHOycHO3d3JySktO++OrHZYvnDgjuw+awbtyI+GLLj7W1Qu0KaVnaIW0yC4jJpDdVStC1/EV0hEYZEmQw6UpqbBgtoylreNrU8WtWLaPTaTk5eYcOn7KyNB8xfLCdrfWIYYPfW70++n6M2tfNe3PGO8veBoDq6pqDh09yOJzxY0d4uLtMnTx24ZL3ExKTld7L4/EO7f/jXuSDa9dvT5k01tzcdMa0CV083RctfZ8coF757qLZb0wBgNKy8gOHjuvz+ePGjpw2xWHC+JGfbfo2NT398ZO47v4N0/iuXb/95GkcEZ2hoQNIi/bkaRwA3H8Y083Xy9PTDQCuht/KzsllMuk6Rvb9VUtnTp8IAHK5/MLF8ILCol49u1NVrSmXLlow+625M8i/tbW1xMWRg38aGjZsmGVEuX573ox331lAaHL/wRNcDmf8+JEe7i5TJo+Zv3B1fEKy9nQnFBtx78HV8FvTpowjFOvp4TZ/0WpCsb16+v/521Zo2LlX3rJXJyWnxjyOo06jvHrt9qOYp8R16JA6/fft04soACKjH/n6enXxdAOAK1dvZmRkr165+M3ZUwGgtLR834Fj+vr8CePCpk1xmDBu5KeffXPp8g0A0Ofzunb1HDt6GNRP9Ny7e/ut25GPYmJDhwSPHjW0pLTs+x//0KKQD95fNmvmJCKm5y6EFxQUBvYKQPuA5wEgaggK7PHB6mU0Gq20rHzOvOWCyioAUCgUI0cMYbGYH3347uRpb1NnENV9lnze4oVvEtffbNt+/sJVAJCIJW/MnMTj6Y0OG0oWACQsFvObbdsvX7kBAIWFxe+vWgoAAd19e/cKiL7/CABGjxpGWH+5XLFk+QepqRkAEJ+QvP7jlWwW67ONH85+c9mipe8f+PcPNzdnABBUVn20/kvCnPH5vMDAHsSL+gT1/PX3vwmjn5SceuzQrstXbqz79CvdIzt8aAhh/QFgyzc/nzh1HgDYbHbErbNN6vP3P/e8eJH47dcblCo04yfP/XT96iGDgutq5Yy6IoTP5y1dPE9Jk1Kp9I2Zk/g83rgxw5NTUtWX9zQ1ii0uLiEU2yOgW98+PQnFxjx+OmX6/J1//mBkZEgNUnNfnZKatnhZI/2v++RLuVzOZNL5fF5QUJ3++/Xt9fufuwHg+o3bqWlppP5HjxpGWH+5XLHknbr0TUxMWf/xSjab9fmmtWlpGalpGfkFBZs+3+roYOvv5wMA+vr8P3f8u//gcRqNdvfWWTaL5exkT4SfOgOILI+HDw0hrD817XbtPkCmnZbCG9GhS4feLHe6VCpX+6MKKblregR/2n8KeYOZ3v/Pbw8iL1N/9na2dfcUjTS8ZNFcYi7dhQvhpWUCwvHuvbreCUcHO3c3V9V3GRsZs1h1pXtxcSnh6OvjRbi8iE9qCJWC3C5KcP7CNcLxSewLMqheXdwJx7fnzSRcnjx9lpiYRjieOnNJJBYDAJvFmjNrqlgsuxJ+ixAzNNDv6tWFEOvXN5DNYtV76MHn6xPuQ4eEAMCZc1eaFdm5b9ZV4QsLi4+dOEeIySiZViFXaEoFkUhy+eotiUR5UkRlZdX162rmqpuZmZKaLCsrJy58fes0mZCU0uQ3WVEhIKw/ADx7nkC6e3f1rAutQpGekUW91eJXy+WK8Ou3Sf2TKT4guA9V/2RJM3zoIAA4fzEcAObXp+/T2GeE9QeAc+eviOvT983Z08gXFRQWk9eHjpwCAEtLc6lUKhSKjh7TVgzPI9OuqPjUmQtosrEF0BmJuPegsrKK6jIgOIjH01MSMzcz7ebblbiuqq5xcLAjK5WkjJur84v4JKUHc/PyRWIxh80GgPUfr9z8xfdR0Y+++PqnYaEDs7Jzz56/qj14NdU1lE5zPQCws7V2crSv62pISqVML5Okp2cR3Qh9+/YCgGvX7yxZ9GZ9pAKfPH0GAEOHDKScwkjrE9SD6E8YMWxQRYXgXuRD3SNbWFhMvA4AHsY8lcsVLdC/VColvFUogKzuCIUNCzXkMgXhnpWVS2ryow/frdPkljpNnjl7WbX5VW/WG1WhiAuBoCHRORwOtaYlFkuU5Fv26qvhtxctmFNf2e/9KCYOAAaHBFP136tnd0L/w0JDKioEd+7et7K0dKxP38TEVDJgUqkorT59+/TpSbpTazMikQQAcnIKBg+dQqOBUCjSpAEzUxNPMu0ePRWLZXU9onTlwhtNRMvQpDpN7lgA/Df89MtfCY2HNM+c/Ee1ACDa8gSLF85ZvHCOkoBMJnsS+1zVf4lEunPX/mVL5hG2+49fv03PyDpz9vLefUcrBM3b/5VoyJPWgRgAoAqUlpaRxRWfz0tMSs3KziXWNAwI7rP917+5XE7/fr0LCosszM3odDrRDX3p8g1PD1dXV6cjx85IpVLdI0sVy8nJb+uUakVNauojat1Xt0z/uqdvdXUNaFzSJdIeo1ecdkjTLQAGgyaTKVARrycG+vyGhvbhUw8ePlESKK8QZGXlqH32r537JBLpwvmziHLF2clhxfL5b8+b+cWWHy9cvAbN3tRFj2KYJNBojYmEOvZQXV1z7fqduXOmAUAXTzcLCzP/bt5cLuefvZd69vDr2cOPGAYAgBHDBwMAGRgdI+vp4UK6aDFGrUjravIVvLoF+m9W+rY8PxvwX3HaIU13ATEYTVRFlASalEearPExGDQlNdLUaVgsaeiUSEpJvXbjdpOpQ+WfvYdOnjoXNjJ00sTR7m4uxAf85eaPc3Lznj2L1/JeOsVPGo3GYNBEYhFlXxEW9aVsNou8FgprGQzajZt3CQMEAAODg3r3DgCAS1euVVVVEQbI2srC3c1pxLCQ/PzCp7HPCN9aEFk6vcGFw2VRVa1jLtVhqwJQKBR/7zl47MS5USNDJ08a7eHeoMmsrNy4Z224cU3LXh1eXwAAQHC/wMDAAAC4cDFcIKgk9e/i4jh8WEh+fmHM4zgAqBUKKdMBWNBodgCrtay2pv46Jgu7InAMAFEhs37yNQCYGBvp/qC+Pj9kQF8HB7tnzxMOHTl1+OjpWW9MXvXuYqILeNyYEc+aabao7QwTE2PqLdP6v6WlZYSBiI17UVRcYmFuBgBDBgcHdPd9EZ+Unp4lEUtWr1xCCC94e5a9ve2efw+RHdk6RrakpJRSozTQFCrdaSj2KPN06fS6EpqqySPHTh093kiTE8aPfBGfoL5o0a1kVfvAS776+fN4Uv9DQwcQ+s/Mypbdlq5ZvbSuk23BbAd72z3/HqLTAYCWk5PbkKCmxtSAmZk2pC9RwKvWZnTUQFlZGVBOqSUfNDc3aUHhjeheHdTkTpfJFGp/0KjvtZG7pkfwp/1HHRNTc1edwtMzckjL26WLh+7vcnJ02PzZ2oXzZ/frGyiTKaRS+Z5/jqSkppMmW/t75ZQMoFDUhSQnN58ciSUl6XSGs7MD4R4R+YBwlErl129E1M/6D+TxeOfOX5XJFJlZeUnJaYT7yBFDAODchWvNjWxCYirZKeHs5EC6d/F0pw7AatcPOVCpp8fV/kW5ODsSmgzuF0hoY+++o6QmzUxN2u5jbvGrFQrFzZuN9H/hYjgA5OTmJ6c00v/FS3X9SFnZuWT6Eo1FcgSeTN97UQ9fMkbJKWkNaVfvLQB09fJA2w24FQSiyo6/D0D9NCFiZK++8qv/7ZZP35gxUfvjbq5O5JwT4/pqdWxcS3otdu7aT1wEdPd1qf96w0aGcjgcYsRy955DDb0Q125TG/4XL18nrm/cbNjoODU1Q2lxry6Rra0VXqvfWzgosIepqTFRm1bdUlsL5LwpN1dnJpNNlGRjx46ghrmuLKzvtXBxcSJcmEw2qcmnsS80lve6lawNVSuV+kGLXy2TKa6EN9L/+Yt1BS1ZMBP6fxGfQj5Cpm93fx9HB3vCcfiwIWT6/r37oI61GU0aqKqqbUi73j2MjIxkMoVcDlMmjdW98Mafllq7LpV46o9hyFdfA5o+vKF8Pnw5i7z28zB6mixAu9y8YpZOHzyo/8wZE3l6dUNtNtaWBYXFBQWFCgXwebxpU8YOGzqQmKHBYDCMjAzz8wuJqd8JCcnGxoa+Pl5MBmP0qKHWVhY+3l0mTghb/9F7bm7Ot29HPX+RqK5rmxYysK+RkaGVpbmNjZWPd5f3VixwdLADgLhn8V99/ZNUKqPT6aGDg8ePH8nlcIi6Xm2tMCMzm8lkvDFjYnd/n7reJD4/LT0rv6Dw+YskIiQ0Gm1o6EBjY6PQwcGLFsxhMBgSiXTj5q1R0Q1rkvPzC2dOn0DYjuj7MYePnCY7kcntB/YdOEHs/UCiY2Tj4uJHjhjC4+mxWMyBA/qamhgveHuWgQFfLpfr8/kAwOawY2NfFBWVaEqR3Lz8saOH0el0Dofdu1d3Ozuble8uYrGYVpYWdeEvKIp5HCeVSnXRpGpyN0uxCgV09/eZNmWcgYE+Zbg7VywWN/fVL6P/F/E6pS+NRgvuHzRt6jj9+kF7uVyelp5J3f6BTqcF9w+aNCGMy+UCAJPJKCuryMrOFYvFL592iCaUjLMmG97IULjam6vdKfTYtn7k9eQ1DbWG2WEOey9koa6bhZur87HDO1Tb6f1DxtXU1E6aOGrD+tWgMplv2sxF5N/gfoFTJo/x9fUyNjIUicT5BYX3Ih/u3X9My140TCZz8KD+w4eFBPj7mpgYVVfXpKRmXL5y49iJc0QzXG2otn73q74+f+niuUruI0fPzC8oIirdUyaN9vPzNjUxkctl+QVF0fdj9h84npaunCs+37SW2DBg4+Ztp05fJN0vnjtgbWUBAKPHzSa7HajoElkrS4tlS+b27x9obGRYXFIWfu32b3/s+WLT2kEhDfl23KS5mZk5mvTTs4ff8qVv+Xh7ymTyxKTU4yfPX7gYHnHrLLnsQCqVho6YVlEhaFKTuiS3FsVKJNLwy0eU3I8cO/Pllp+a++qX13+T6WtkZHgz/DiozFb6329/a9fAjl37t/+6q1XSDlGLknHWZMOVCwC1Nw58FUhez1wXTV5PC7U5HJ6Hukago2+hjiAdvgDAWUAI4Cl6qASkcx4JiSeCIfjBIAjgOgAEAez/QZDOcyQkTgNFEATprC0AIyODJoWUZHR5BEEQBHnFaDLOmtyxBYAgCNKJxwAqKprY1baxgHGT8giCIMgrR6Nx1uROR2uOIAgCuBcQgiAIggUAgiAI0tELANx6G0EQBPBEMMATwZDOBx6JiuBKYAQBPEQJQTrViWBMXao/qmcLoKIRtP4Igi0ApCNgaWne1ctDIKgkjgiHTt//02kVgnTI/KzJHQuATs3C+bP69wt0c3UizqI6fyG8k9s7VAjSuVoAfL5ek0JKMro8grQLFAqFkaEBeRIhk8no5ImLCkHaNZqyqyZ3XAfQqTlw6MTHn3yJekCFIJ13DKC6ula7kJJAk/JIOyItreEMOalUhomLCkHaL5qyqyZ3OuZvBEEQnAWEvC64ODtMnzY+qHcPCwszDodTXl4Rn5B85tzly1duQsMG34Yjhg9ysLd1dLBzdXFMz8j+dtv/5r05vW+fnhYW5lVV1Tdv3/vhpz8rKgRKnocM7Dttyjgfb08ej5eXX3DnTpTuAevV03/q5LH+ft6mpiYymaygsCj6/uN9+49lZGYTAnweLyCgm7ubc5cubn7dvI+fOHf85PkVy+YPCA4yNjYqKSkNv37nz7/+rRA02oLQ09Nt1oyJPXv6W1iYgUJRXFIW8zj2n71HExNTXiam2r1tFYXoohM9Pa6bq7O7m7OHh6uPd5fMzOwNm7YCgJWlhZubk4e7axdPN18fry3f/nIv8gEAGBsbjRo5xMHBzsnR3s3NKSk57Yuvfly2eO6A4D5sDuvGjYgvtvxYWytUGxgtUR4zatgXm9cqyX/97faDh08OGxqy9etPScc//vr3tz/26KjA/v16T5k0xtfHy8TESCKRZufkRdy7f+DQyfz8QvyQsQBAmk2foJ4///AFm80SCkUnT1+QSmUTx4cNCA4aEBw0OKT/x598RYgZ6PO7+XYdO3oY1E9bPLjv97sR9y9evjFpQpiJidGEcSO9u3rOenOZRNJwGtwH7y+bNXMSAMjl8nMXwgsKCgN7BegYsNUrF785eyoAlJaW7ztwTF+fP2Fc2LQpDhPGjfz0s28uXb4BAD16dPt2y6d6elzikcDePSaMG5mYlHby9MXxY0dYW1vOmjmpX59ec95aUVVVTcjMmDbhwzXL6XRadk7ewYMnLa3Mw0YMsbO1HjFs8DvvfRwVHdOymDbpLSH28YfvTJ82gVDIxUvXCwqLevX0p+yUQudwWFp08u47C2bNnAwAZWXlBw+f1NfnjR09YtoUhwnjR27+fNuV8FsAsGTRnLlzppOPCIVCws+jh/4iR5sBwMzMmHA3NTH09fEaFRZKlql792y/czf68dO4wSH9R48aWl5R8fP2HaqBmTp53OqVS+h0Wk5u/pGjpy0tzIcPG0REedWaTzOzs5+/SPTu6gn1w91Xr92Offacw2Hl5eXfuRvVv18gjUaLvv/owcPHHA5Lu2/3HzwGgLlzpi1b8hYAVFfXHDpyisPhjBsz3MPdZerksUuWf5DQuKBFXgGasqsmd6b2/K32YV0eQVrM0sVvstksANj5975/9h4BgGfP47d8sR4AwkYOuXrt1p27UQBQVFz8xVffOzrY+ft5AwCHw/n8yy2ExUlMSvn6y/UA4OnhOmL4oCtX69oNw0IHEtYfAL7Z+svJ0xcBYM+/h29fP9WkvRsVFkpYf7lcsfzdj1LTMgAgOTn147XvsdmszzetzcjISkvPjL7/aPzkuccO7zLQ5wNAUGDAmbOXv9jyAwBcv3ln/z+/AYCLi+PSRW/+/L8dRAmx9oPlNBqtrKx83vx3KyurAIBOo40YPpjFYn7y8cqpMxe2IKa6eKtQKIaFDiSsP1UhbDabVIh2RoWFEtZfLlcsW1Gnk4SE5I/XvsdmsTZ8uiY5JT0tPfOPv/598SLp668+UXp84pR569etGhzSX8k9Jzd/0xfb7OxsiPjq6/P/2rX34KGTNBrt1vVTbBbL0cFONTCBvXu8v2oJEeW5b68goqxQKIgor13zztSZCxctfX/Pzl/c3JwBQCKVfrP1F0IsKTl1zdpNJ478zWazV63ZKJVKdfGNx9NbOH8O8fat3/964WI4AEgkkpnTJ/J4emEjQ7EAANwNFGkuPF7dhC2vLh7ERUxMLOU7b1RhLywqJq8JmwgAz58nkI5dvTzI6zfr66FFRSWnz15qVqjemjuTuIiNe05YOgA4fyFcLBYDAJvFmjN7KtQdPSHIyGgYRz124ixxkZKSnpycVmc6Rw0lLpYsepNGowHApcs3CCsDAFHRj4gLBwc7T0+3FsRUR29JhRQWFh85dlYkkohEErFIQvojl8kJR7W/eW/OIMSePH32Ij6ZcDxx6qKoXiezZk4WiSQ1NcKLl2+QrRO5XEFIFpeUX7l6i3yXVCKjek7tQtm777hIJDE2MpJKpEKh6ODhU6qBWbRgNhHl8xfCi4vLCMc7EffJKLs4O1VV1R47cY5wYbNYIQP7kY/3CPCzsbE6cepCdXWtjr4Z6BuwWExSgYSMT9cu9UmTqEV1+GuLHwAo/SXR9AiTuKf941f1C81027HnnyObNq5hMBhFxSWEqktKy8m7+ny+qLGFUk2XsvKG3nA2m024m5maeHq4Eo4PHj2prRXXSShoSvZONUh2ttZkrTMhIYWUEYkkaelZXTzdACAosAfpLpXKyH6G2LgEmazub0pqhru7CwCYGBuZmppKxBIf7zp7USGotLS0rHsfrSFIjvb2T5++aFZMzc1MdfE2JzufVMjDmKdyefM2OLGztXZytCeuk5JSSXexWJJer5O+fXtRJhRJSXNJIhFLdFveKQOA/IKiwcOm0GggFIqUBMzNTLv5diWuq6prHOoTi/pGN1fnF/FJFy9ff3/VEgaDAQCjw4YeO36uvvtorEKhOHHyvO6+XbpyXSQWc9hsAFj/8crNX3wfFf3oi69/GhY6MCs79+z5q/gtt4MxALTmrxtnz1+JjHqor89Pr69HU20Tjda8vWtIeaLhX9fJkJPfLE8c6y0dAJSWlVNvlZaWkTaIz+dVV9dQ71ZWVpHWHwDIfn+iQOLzeeTfxQvnLF44R9XwPYl9/jIx1eLtyyjkZXTyMohEIrXuOkaZGL+JjHrUv19vAAjo7mtjY5WXV2BhYTZwQJ97kQ9zcvN1900ike7ctX/ZknlEcfjHr9+mZ2SdOXt5776jSoP8CA4CI81jQHDQiuXzbWwsORx2q3hoYMCHhknBzTNJfF7DMkKJpFGNQUypw6oaO4lUSv2rAAV1fJUYJyA4dPjUg4dPlN5bXiHIysppdkx189bTw6XFCnkZnbQFzdLkuQtXiQKARqONDgvdsWv/pAmjGAwG2Vmnu29/7dwnkUgXzp9F9Fs6OzmsWD7/7Xkzv9jy44WL1/ArxgIAaTajRw39dN0qLpcDAI9iYh8+elpaWv7eigUv6a2mLg4mq+k8UCtsmHTIYjUaJab+bdLSUav8lZVVhvoNc2ASklLInv2XhNpDosVbqkJYbCaPx1UNJIPJIN2VH1fIqcM2VDEi7eo7weR1t2jk/qN0UphNGXJnc1hUTxhMBsV/rvYoU0vWtIzMu/eiQc3YUp0nUVEPa4VCPS4XAMaMHnbw8MlJE0eXlJRG348hZJrl26EjJ89fvDpi2KDx40a6uToTCvxy88dFRcXPXyTi5wyvegSR2yx3ZpN5S/VhXR5BWoaNtdWmDWuYTCYAfLvtfydPXwAABoNBFgBKJkmtmdDTazBAzHr5qmpq94sxKWxra9WkvSuiDMBaWJhSZczNTchuENLY0ek0sluGKuxgb1tfZZaWlpayKIG3sjTXnq90j2lhUZEu3lIVQi2KjI2NdEmp7OxcyiOG1FsmJkakTl5B9R8AqO0kk6bCXysU3rodOWLYIKLO/tbcGVaW5rv/OUR21unomz6fHxwcZG9n8yI+6ejxs8dOnJsxbcKK5fOJDDB61DAsALAFgDSPHj26EdYfAC5dud6KPqempEskEqLC7uTU0H/t5enetLHLycvNK7C1sQIAopZHjgo6OToQ19H1c2y0VP+71E/pefjoCbFoKDsnz97OBgDIWy+Pjt5SFWJvb1tTI6xfhedIysikMtJdicSktJzcfDtbawBwdnIkxVgsJjk4HHHvPukurBURlW42m006UmcciUUS6rtk0oaxE01hoAYmKyuHGK11c3NpUv7MmctEAQAAb82dKZcrDh89Qz6lo2/ubi4b1q8GgMNHzxDTmXb+fWBUWCiRQ4wMDYgH+/frvfmzDxVyxfoNW8jlF0gboSmxNLkzdcleSgJNyiMtpqiolGIx+SUl5QAwcXyYJpOk1kwwGEzqbjaEe02NMPz6nZHDBwNAr57duVxuaWk5jUYbP26kLvZux859Gz5ZDQB+3bytLC3S0rMAYNzYEcQQhUQi3bFrP/ks2buiUChIx7fmziSsrUKh2Pn3AcL9r537Nm1YAwB9+/Y2MzXNqq9WGxjof7pu1eMncfsPnmhuTHX0lqqQoMAepqbGhEKmTRmnY2Lt3LWf0ElAd18XZwdCJ2EjQzkcDqGT3XsOkcIv4pP69e0FAO5uzhwORyQSsVjM8eNHtlbO2fH3ASLKA4KDHOxttWgSACIiH5SWlpuaGhO19bsR93Nz81vsm5urE9StEOKQ7afYuHji4u15M81MTQDg7XlvYAHwusHQY6tvBEwf7kBeH77cMK3bz8PoabIAFddGlJaVjxk1jOiGDurdw8zMdO6cqePHjRQJRUTPskQqvXUnsqamlkajBfcPmjZ1nH79kJ1cLk9Lz5TLFdOnju8T1KOuc8PQIL+gKDsnVy6Xx8XFjxwxhMfTY7GYAwf0NTUxXvD2LAMDvlwu1+fzAYDNYcfGvigqKlEN2Iv4JGNjQ18fLxqNNjR0oLGxUejg4EUL5jAYDIlEunHzVuq3PWF8mLW1JQDocbm+Pl7OTg7Tp42fOnkscfd/v+0+e+4K1E0qTSa8ZTIYo0cNtbay8PHuMnFC2PqP3nNzc759O+r5i8QWxFQXbwHgZRTSXJ3k5uWPHT2MTqdzOOzevbrb2dmsfHcRi8W0srQgBPILimIex0mlUk3x1bT9g+6aBMqu19bWlt18vYi/P/78V1p6ZnN9o9FoIQP7GhkZWlma29hY+Xh3eW/FAmK6cNyz+K++/omYDTx+7Aii7ZiXV3Dm7GX8xtsOJeOsyYZTodlbGqu9cWxbP/J68poI8np2mMPeC1mo67bD1dVp+ZK3/P29jQwNi4qKb9+J2rn7wIdrlg8dMoCsX4+fPE8gqLwZflzp2b927svJyftswxol91lvLn/2PAEArCwtli2Z279/oLGRYXFJWfi127/9seeLTWsHhTQk97hJczMz1c+9CQrsMWXSaD8/b1MTE7lcll9QFH0/Zv+B40Tll2T3zp+6+/sAQHV1zcVL1/v3DzQ3M6msrH4S++zAwROq1cDgfoFTJo/x9fUyNjIUicT5BYX3Ih/u3X+MWAxlZGTYgpg26S3UDRK8lEJ01wkA9Ozht3zpWz7enjKZPDEp9fjJ8xcuhkfcOkvOr5dKpaEjpgGA2vj+77e/m8w8ukSZwNfHa++e7QBQXFw6YvRM6mxd3X1jMpmDB/UfPiwkwN/XxMSouromJTXj8pUbx06cI6dCEV1AAPDpxm8i7j3AD7ztUDLOmmw4FgBI20IWACWlZaHDp0I7PECjMzA0dOCmDR/s+ffwnzv+xUzbAZg40PzErYb5Gv9satg14M2NMXgiGPKKoNPp5CwgzC2vLWEjhsjlijPN3BQEAZwFhCDQ0c/Q6Hj079f7vRULI+7d//Hnv6wsLQJ7B0RE3k9OycA80GkPhMETwZDWRy6Xk4ONmFteHxYvfNPTw7WkpBQA3pg5kU6n7/nnMKqlUzfW8ftEWhEWi/nGjIleXdzrlxEZf7pulbe3J2rmdUBQWQkA/n4+mzasmTNryvUbd4lt/RHcDhpBWgEnR/sP1ywn90Kg02mTJ43+8P3lqJnXga3bfo2+HwMAIQP7njx1cf2Gr1EngGMACNJaJKekd+81FPXwepKRmb1o6QeoBwRnASEIgnQ0NBlnTe7YBYQgCNKJu4BwFhCCIAh0vmmgOAsIQRAEcBYQgiAIggUAgiAI0uELAOqZSgiCIAh0qkHgJssAJQEsM5CORG2tCJWA4EIwBOmMYIUG6fCZWZM7U5fqj5IM1pgQtP4Igi0ABAHs/0GQ1zw/a3LHWUAIgiCdtQXA5bKbFFKS0eURBEEQ5BWjyThrcscWAIIgCOBCMARBEKQTwRQKxU0KKcno8giCIAjyitFknDW5YwsAQRAEsAsIQRAEwb2AEISCoT7b1pKPekAQwL2AsMzo8HA5jCB/Kxd7Q3cHI3cnIzsrfkZu5ewPrrZy7YNOGzPIaXiwo7OdgR6HUVgqjH5acOxSSmZeFeBaMATBlcDIy8PXY17cMVZH4U9+jLp5P7eLi8kX7wVR3Wk0WuuGisdlfvdRf18P04ycysUbblTXSrd/OmDSMNewAY7zPr6WW1gNuBsEguBeQAi87MQA0ZIN1xZN9+3hY0k6PogrFIqk5N9APys2iwEATAbU1oqiHueMW3Jm69rgLi4mhIBCrmjddP94UYCvhykAHLqQmJxeymLS5XIFAOhxmW4O+ikZpWj9EQRbAMjLolDAg7hCOv3Zr5QC4MvforMoPS2r3w54Y0wXAODrsYhHcgurr9zNJAuA1sXUiDtygFN9+SQFAAM+W4/LBIDcguoHcYWA/T/NYeM7QWOHuNyMznn/69uY4QH3AtLZHQsABABg/+mE+gKgIUvUCKVt9Dpne0M6va5Pyc/L/PS1tNIK4YSlZ60tePlFNVKZHFNEd96c2HXsEBfUA9KSFoCxsWF5uQAV0cnJL65JzxE42xny9FjwSoYlyOuePlbEhVQmz86vwrTQHWNDzuq3AkaFOKMqkJZ3ARkbGzaRzxoLNCmPvIbo6zeax2looG9c22gVyOe/PTY14qRmVZLpy9PjknfpDPq8yd3emeWj6vP3u2PP38pi0GkX/xpJHSs+eTV9+/7nSsJDgmwD/SwCu1mQLg42+g+OzyCuJ6644uthYmvJt7fiO9jwXewNbkTn7TgSPy3MdVCgrZWZXkFJ7b+nkq5F5ZKPB/lZhva19XYzMTFk02i0iipxUnrFlYic2w/z1eqhufLaPh4GrYe3ua0V386S52DNd7YzuP0w/59TSRNCnfv3sLKz4tMAcgqrr0bkHL+SLpMrTI04U0e69vG3tDTlSqTyZ8ll/55Kjk8rp/rJYtJHBNsP6GntYm9gqM9WKBSV1ZKMnKrIJ4XnbmUKRTJScnqY6+xxHnocBukSEmhHavLg+ZQdRxPaItbIa10n0GCcNbljFxBSR0ZuVUautgr45YgcmVyxbKY3i1lXcmTlV2/7+2lSegUAyBWKd7+8N2aw44j+9lKp/Ks/Hz96XqLqiYMN34DPyiuqNdSv255QJlc8iCuqK6V4rLkTPN0dGzJrQFezXzf053KYZQIRm0V3sOZ/vKh7UZkwNrGUx2WuXxIQ5GcBABKp/EVKuQGf5WJvYGHC7RdgdS0qd8ufjxUKoM47apa8LhXwtyd3oYY2uId1SG8bJoOWnlOlkCv09JhuDoZu0w27dzU/dCHl83d7lQtEgiqJvRWPw2YE+Vn29DZf/9ODh8+KiccN+KxtHwa5ORgCwMXb2X8diWex6EtndA3pbRPgbTZlhMvHP0Sn51TVF8/MpwklLnYGlmZ6hEtJuTA5s641n5lX3UaxRjpUC0CX/p/GMkbYZdQeqarSo/4VVFaVl9eZkg8W9Jg+ynP6ygspmRVUmZpaIXktl8lz80r/PVFqYcIiRgsAgMWkRTzIIM3HvTKBjTl7RH/7a5HZp68mqg3GT7sfAsCAXrY/rBtIuNQKpcs/u0YKzFh5fvVbAW+MrXuFo41+xKO8Nd/cEUtkX6/pP7SfA40GPm4Gd+6nf/neIMKulVYI56+7Soxpf7iw57QwD6KpERmTe/xyMtRNY4VmyetCeblAKbQWptyLtzM+/1+0SCxztDU48tMoBoMGAEF+Fr18zLfufHj0YjIAzBrXZdW8AABgMukrZnlPXH6W0OGs0X6E9QeAn/Y8LCqtBYBNv5Rf3TORTqNZmHI/XRIwbeV5mUwBAD/segAA65b0mjTcnXjk4bPCdd9FQKPJu60fa+Q1RqNx1uSOW0F0doL8rScOc9dd/uLNDPLa2pzn18WcenfCUFcAOHrppQzK4/hi8lomU3z+a7RYIiPqvIRjamZF2EDnIH9r4u+/J+PJGU1HLiSRz84e34W8bq58y0IrFMk2b48SiWUAkJlb+SSh4VZ4ZBZh/QHgzLU00t3eWt/F3oi8Jt37dq8LraBKnF9UQ1w72Rl072qhe9jaLtYITgNF2jE/rQ/hcpnGBmxi+r/uPE8pzc6vIk3ViGCnJ/UW0M3RyNvdND1H8OjZS83jlEgaerprRVKiIgwAF29lgALuxxZcvpv599fDSJmopwUNHVk5lXK5gphi5GhjYGPBzyuqBoCpYR7Nkm9ZaKUyuVjSMIWpsKSGvK6satiOUVAlFopk3PrueyszXmpWBQDcfpA7rL9jXXeQfsMJHlXVYoC6IRwnW8OHOk+TbbtYIx2kAOBwmp71oSSjyyPI6wab3aiwLyyt5XIYYrHM1opPp9EIAaWUZTIZ1M4E8u61yOw3J3gR10P7O/6yL5ZYwzVphAcAnA5PazKHsFhMLTlK093wyJzwyBwAMDLU83E3bWjeCsRUH2qEUn1e3V83J+NSgZjLYTZLvlmK1RIXat86g0mn3pLK5ACM+sVoLOJWeGSOWHqvp49FcmbFuRvppDy1i16fx6L6Q2c0NOIZ9EavaNNYI68nmj49Te7YAuikbN0ZQ8y59HYz/fWzEHJcVxeuRjQUAKZGnB7eFg/iCllM+shgB5FYdv5WZlsH3s6STy4jAIDzf6nf6EImUxBbSjRX/j/k9oPc2w9yAcDMmGtlxmMy6QCgx2G+Ai0h2AWEdDqep5ReuJUxrjkriVKyKtKzBc72dcOVQ/vZP4grHNDL1siAc+FWRmV1m9cl+bxG+TbySQHRBFEiNrEkp6C6BfL/ITYWvDfGeob0tjMz5r5iLSGdsQAQiSRNCinJ6PII8rohFksb/5WQ6bh5e9Tm7VGqj0ilMmpvBjXdL93JWDyjG3Ed0tvuy1/vjwpxBIDD5xN1yR4SiVRLjtJ+FwAElY3Wta/deqe6RttLmysPzRsD0BhaGWVJs1wm16QZiURK3Ar0s/ruowHElhg1QunZ62nxqWVFJbXvzw9wtjOsT5RG/sgpr5DJG91q01gjryea8pgmd2wBIC3h0p1MsgAw1GePC3UN8rdOTC+PTSx5BW+nDq4CAJ/L1G7amiv/n8BmMb5c3Y+w/kKRbPb7lzLzKqFupmy3V6AlBPBEMATRhczcysS0MvLv6rcC6DTasUuvaDp5mUCUmVtJ/rW3MWhd+f+E7l3NTQzrtiZ99LyQtP6vTEvGhpw/Ng+5vX/Kdx8NaO7cMKSdwmTpkNJKMizMHO0xpZkMpb9NpiODMsOERlNO96v3sj3r9wrlchi1QunVe1k65g2qz6o5SvtdgrM30pe9UVcvDu5pq9rycLE3TMsWtEw+uKftmrcD+HqsG9HZ3/z1SPvmdFpCSx2DpdPpmpTDYNBZLIahQcPG1CKxjBRmMGiGlCmhdAaN6o+cEjQjAw55i0YDhaJ5sZ4+2rOnryUAhATajRrkfO5GOn417Q5NeUyTO7YAOikvf77L1YgsaNwpVFMrbaWwNR24IxeTMnIq62e7u1PXoxkbcN6Z5bd32/CZYzxbJr9mfoCVOU+fzxoz2GXMYOcWh1bHU3QIsfSchuIqwNvCyIANAPo81ler+tpZ6Wt6NrugYfeOXj6W00d5jBzg9OWqvitm+zc31naUUz+1vBHpaLOAqCtZoKmlLrrII68bw4MdZ4z2pLpsWxt88Gzi5buZ5QL1G4WPC3WdPbYLZQ8fg3WLex27nBxXX4vMzBXEJZb4epqRi0t1yRg8LrNPd+uF032BMrH9i5V9LtzKePSsUFAlHjfERenuj+sG7Dr2XGn1k0QiW/H5jR/Xh7g6GOpxmL9vGpycWV5SJjQ14rg7GdPptOinBRduppNBapZ8WpbAyoxHPOhsZ6AlXppC+zS+eM4Er9C+DuSt0YOca4SSfacTGHTaijn+5Bx8APhwQQ87S/7xK8nh97KIR4wNOAe+H5mSWeHlaiKWyB7EFfbyrTvLYVg/h8S0sofPCokNIc7fTH97sjfRRGAwaCvndgeAGqH0q9/uSySyZsWa2l+UlVeJn3l7RFOqaXJn6Gs4EWn68Ia8e/hyQ13Pz8PoaTLuBdSe4Okx924bYWXOozoaGXD697SNflqgdgdmnh5z99fDqEaKRoMuLiZGBpwrdzMp1pzVN8AGAJ4llew48kyXwIT2ddiypr/SHEdXB6PhwY6CKvGT+OK/vhxqatTorr21PpfNCL+XpeRVZbXk5NWUwpIaJpPO12NZW/DMTfRkckVETP4v/z75/UCsUotEd/nrUdknr6ZMDfOg02k3orIfvyjWFB1NoY1PK9u2dgCxEVD9Ki1aN0/z2IRivy7mb4zt0nhzbFaf7tZnrqWduppaJhCZm3C5HCaHxRBJZGevp2/8OUoklg0KsieELc14owe5hN/LLq0QAkCtUHo9MtvSnGdsyAEFpGVVnLia+skP9+KSSpob65QsgY+HmYkh515M3u8H4mRy3CKunaFknDXZ8EatT3tLY7U3jm3rR15PXtOww9TsMIe9F7JQ1wjB21O8l73ht3l71GnK/jYdAD6Pdf2fSXQ67a2PrryaqU0I8pIoGWdNNhxwDACB1pm5SJ8+yrOyWnzpTmYHi9pbk7zpdFrUk3y0/kgHhmlgwG9SSElGl0eQDkmQn+VHC7vXimQffx+VkVs1YaizmTH36KVUNofL7kCHq4cNdJw7sWt2fvWWv55gbkfaEZqyqyZ3XAiGNIOZo92NDTnGAECjmRlz5473FEvkRy6ldrBo+nma3nqQ9/3up4Iq3CIN6eizgCorm9gJpLGAaZPySEclt7DK38sMAL77sA+LSTfUZ3//d0xKenEHi+aGnyLkOASKtD80GmdN7ky05ojufLfrkUKh6ONvbcBnZeZWfrfrUYdcLoTWH8HdQBFEmXKBaMNPkagHBAHcCwhBEARpxwUAda0KgiAIAp2qC6jJMkBJAMsMpCNB7KmAIDgGgCCdDqzQIB0+M2tyZ+pS/VGSwRoTgtYfQbAFgCCA/T8I8prnZ03uOAsIQRCkk0Jns7ERgCAIAp20C6jJMkBJAMsMpCMhFktRCQiOASAIqBsmZRgbGZSUlkMTh5EyuVxOW+8swmIx9bhcQWVVK/qpvULTFm9EkDZCU2bW5M7UpfqjJIM1pg5DcJ+ABXMn0+n0yqrqNeu31QpFALB5/TvOjrYymWz3vlM37z74aNVcby+3sxdvHj5xSa0ngwcGDh/c19raIur+0993Hdb+xv59AkaE9rcwMxGKRKnp2SfOhGfnFugS1Ga9RS0D+vVY8OZk1XN6t/+5/3FsvKp8SP/eoSFB1lbm0Y/iduw52oI3Ghnq21hZeHVxdbCz/uWPfQ1fHZM5YcyQPr38GQx6cUnZ0VNXEpLSVi6d4+vtQQicPHft/OVbv/+wgU6nKxSKk+eunbt0E7Mrgi0ApDW5Exnj7GQ3fEg/A33+mJEhR05eBoCNX/3v528/unn34c27DwCgtFwgkUgrBJWaPLkd8bBfoD+D3vSEgr6B/ovfmlpaVvH++q0GBvxvN6/u4uG84oMtCoVCyyPV1bVPnyXq/hbN4XzkaG8zIrR/ZnbeJ5//AgA+Xd3XrnwbgKa2TnP9dnRgT19bG0u5TN6CSs/MKaPChgUT1zW1QqoPc2dOGNCvx287D91/FPfJB4tXLZuz6evft/68e+NHS12d7TOyck+cCVcoFJ9t+XXTuuW/7jgU9eAp5lUEXqI/U5M7zgKCTj9vTE5cjBja39TECAAUCoVcrpDJ6k6R/vPvI4tXbop6EKvJB6lUVlRcpsu7evh7A0BaRk5NrbCmVki+3dPd+aNV87293JT6XiaMGbJ0/vRuPp7NegvovM3n8/gUQWUVnU57yXip5cDR828uXvfvwTNK7taWZsF9A4QicdSDWKlUdjcyhslkjhs1SKFQ7N53UqFQODnYhgT3AoAJY0Ij7z9F64+0YQtAT6/pk5yUZHR5BGk3OYDJqKkRsjksNos1fdLI3ftPAgCNRmMxmXp6nD69/YcP6WdtaR73POnXnQcBwMLcZMr44R6uTnQGPS+/6PjpK0mpmQwGAwAYTIaeHmfF4lm+Xd0BoFYoWvXxN9TaPdGT7mBvra+vJ5fL/tx9NDM7j8tlW1uZeXu5ZWTnpWVkk8LvLHqjq6crAAwK7hXUq1v0w1jiLaYmRssWzOji4Uyj0c5cuHEn8hEhb2xkMGX8cC9PVwaDXlVdc/X6PaIFoxRZAKDT6GQeXrvxe7lcoafH0RIvtW+0NDedNG5YF3dnoEGFoOrG7egbd+6DuiEEQp/kGwP8u9JotOKSMg6HBQCl5RUA0M3Hk8fjFBSV3Lz7YFBw7xmTwkyMDd1dHT/b8j+1n5tqaPX0uKEhfawszRKS0gHAz9czIyt3+5/79fk8JbWkZ+Z8vHoR0RMmqKz+4NNtlhamn69fcfPug/1Hzo0NGzRmRAgAHDx24frtaPxA2heajLMmd2wBIFBZVX3j9n0A6NPbz8HOmnrr/qPY8opKBqMunzAZjHeXzO7ezeuXP/ft/OeYq7P9W7MnKvl2+MRFALh6497qdd8q9e1cuHI7PTPH0tx040fL2CxWN2+P1cvn/vLtumkTRlwKv3vp6h1o3DWfkJwOALciHq7d+P2x01cId1MToxNnr/7w6z/GRgazpo0xMzUCADqd9t6S2QF+Xbf+vOvDDd8x6IyZU0a7OttribW1lfn2resJE689XqpvZDIZ7y2dHeDn9fvfh9Z8srWqumbmlFH9grrrom0Lc1MAqK0V1vUO1QgBgMth6/P5AHDybLigsprH404cE7rv8Nnqmlo1Zba60D59llhdU2tibOjn63nyXPjtiIddPV1trCxU1cJgMHb8cxQAFArY/M1vANDT3xsAunZxBYAzF248iUu4cOU2Wn/A7aCRjoGfj+fmde8QP3Kkkcq5yzera2ppNNrkccOUOoiKiksb+nC6e1uam5aUlqdl5BQWlaZl5ETeb9RBYWJs+M6iN8JvRh05eVkul4PysGovBzsbqUyWlJIR2LNbv6DuUQ+elAsqyyoEx89cJYagmyQtM6e8orKgsEQikdLpNFtrS6JzydbGMjU9m3BPy8im0cDPt4sGE2yyYe3STz9cQlTPm4yX6ht7BfiYm5mUlFYkJKXLZPLoB08BYGRosC7h57BZACCrV45MLqufp8Eimk0nz4UTLkKReoVoCm15RSUApKRllZULHsfG7z10xsrSTK1aHj+NFwpFNBoQZWSAvzfRpjE3M2Yw6O4ujnciY/Cr6RQdALW1TX91SjK6PILAazaV08rSrO6aTqemoFQqkysUJSUVJ89emzVtdNcuru6ujgqFQiKVEmJSqYywU7W1IgszEwAoKxfU1orSM3M3fvW/+nJCBgCGBvzVy+damps+jHmmmklcne3Hjx4CAFt//jv2WZKpiZGdrVVCUrq9nbWDnbXaTCWXyYkAEHeJt8jq/xIFjEwmr60VWVqYAYCLk93Xn62q69eqFRYWlSp5S8SloKjkk89/sTA32bJxZW2tSCKRao+X6hvNTBvkAaCwqJQoV8RiKTl2QiCRSIlhFTIk1TVCAKDVf0dSSZ18haCKcPHu4iaTyxl0+szJo9Zt/okIc6MCTENo61JKJq+tFT17kfLsRcrk8cPUqqWyqiYmNr5vb/+unq7JqZm21hYlpeVmpsbuLk7lFYK8guKs7Hz8atojmoyzJnecBdQpiIh6HBH1WLvM1RuRQwf1sbI0mzE5THWuZP3xEXQAoGuYitPV0zU3rxAA3po9cd3mn0SiRieqe7g5AYCgsir2WRIAlJZV7Pr3BABMHBNaIah66RKODgDPE1K++2WPjo8UFZctWLHR2spcLJZoj5caPTTWT7O2EyooKgEAnp4e8ZfH0yMq/sQSin5B3bt2cf36+50fvDfP2sp89PCBp85fb1Yq6KiWhzHP+/b29+/WJa+g+ElcQmFR6ajhA3y93aUy2b3ox/jJYBcQAp1sOpDs0PGLAOBgZ21sZKBWJj+/mOjnUXs37nnSp19uzy8oJoYoQWWkAQD0uFyi253A3MzY3s76Ycyzlwx8Xn4RAJgYGTb3wXcWzRw8MFB7vFTJzS8CAFJLxEVhcZlS9V+TlhQKhYWFKWHBrS3NACD2WSIRgDkzxv6z/3RCUtqpc9cBYOyoQRbmJs1KBR3V8iQuQSKRmpoYhQ0Ljoh6/PDxcwDw8XLv3s0r+mEsfg6dpQvIwIDf3NWbs8McUHEdBi9XQyN9dn2aCspK8kzMbADAz92IH+YAAF2c9AHAwYo/O8yBwSgV1laZGBuuWTA4PyeZq6dv7+ydEn/f1Y4PAGYGMH2oTVbSXWur8cOH9DVlF5aV5DXUNejlgvIiQ2OLTe9PTYi7p1DIuXr6fr2H11SVGtHT1WYqc0MFAPTt4WbB68tisS1s9AHA1Y5PCDOZdAAY1NOiq40DnVEmrK1ydLBZMTuwrCSPw+W7ewcmP48WCRvl7S4uBgBgYlAXX30DE0d7G2FFhr5MQ7zs1b+xqCBTWFtlbma8Yk5QeWl+r/5BAFCSE6saC/eutgDAYTHmjXOTSuqaRHlZCbaOXqvnDyrMTekdHCSXy2SCF3NGO/XoM0oqEriZl7qFOdBoaTXVAh7f8ON3pz+6d65xvV59aInYOVjxyGBoV0t5SbaFtbMBn+ttW62wUYhFtTyeXklh9tj+JgAm+Gl0loVgBgZ87UJUgVN3SlFrHYbAnr6hzr4MBt0ncNKWH3YoFPA44+IH775Fo0FCZu25O6Xd/byGOHQFAAtrJ7pxwPGz4Tdj90waE+rSpY9L135FxWWnzl0zNrYeZOsGABa2rtVPPE+djNQz9fb19gjoO3bfkXPU6uT5qN1DBgYGdPMKHDxLKBTJZLLb0S/Cb0RKpOpXqTxIvTV/tpmpqYWBZbek1IxuvdwBwMrOvfJRhq21BYPBBAB334GXI/PSM3Mi4/dNHDvUL3CUWCKpqq69euNe5P0sqm+9e/gOdvYBAAMjs+ETlpLuMQnltyIKVeNlZmYbaqfpjYdufr974phQd9/BXA5bUFV95ORlpWmgI4cGDw0J0tPjAgCDyQoZ+VZqevaPv/0LAOciT44dOah3jz6uXv2KS8v/PnQgKSXji09WEJV6tnmvIycvTxo7lMc3BABzK0djh+A9B05TPVcNrb6BxWAnbwAws3SQ63c7c7Fu5bAWtRSK4mZPd74f8+LErSIA4Nsk9Avsfv56TOR9/MbbK5qMuSZ3Wlc3O7U3dm/0J6/nbXqCmkUQBGlH6GLDmbr0/7T1Dl8IgiBI26HJhuMgMIIgCOBmcBo5tq0fagpBEATwRDAEQRCks5wIhiAIguB5AAiCIAh0jJPCaPaWxqgdBD8MBMEWAIIAnqCEIFgAIAh0gn1SaagEpPMWAPgBIAiCYAsAQaBTboOqQCUgnbcAwA8AQRCkE0JH648gCIIHwiAIgiCdiP8DWmWsroFRkrQAAAAASUVORK5CYII=";

        private class Store { public Dictionary<string, float[]> Spots = new Dictionary<string, float[]>(); public bool SignsFlipped; }
        private Store data;
        private Vector3 centre; private bool built;
        private readonly List<BaseEntity> parts = new List<BaseEntity>();

        private string MapKey => (World.Name ?? "") + "|" + World.Size + "|" + World.Seed;

        private void OnServerInitialized()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<Store>("EchoMilesTower") ?? new Store();
            Collect();
            if (parts.Count > 0) { built = true; Puts($"Miles' Lookout is standing at {centre} ({parts.Count} pieces)."); }
            else timer.Once(10f, () => { var r = Build(null); Puts(r); });
            timer.Every(60f, KeepLit);
        }

        private void Unload() { }   // the tower is saved with the map; nothing to clean up on reload

        private void Collect()
        {
            parts.Clear();
            foreach (var e in BaseNetworkable.serverEntities) { var b = e as BaseEntity; if (b != null && !b.IsDestroyed && b.OwnerID == Marker) parts.Add(b); }
            float[] s;
            if (data.Spots.TryGetValue(MapKey, out s)) centre = new Vector3(s[0], s[1], s[2]);
            else if (parts.Count > 0) centre = parts[0].transform.position;
        }

        private bool IsTower(BaseEntity e) => e != null && e.OwnerID == Marker;
        private bool NearTower(Vector3 p, float r) => built && (new Vector2(p.x - centre.x, p.z - centre.z)).sqrMagnitude < r * r;

        // ---------- where it goes ----------
        private Vector3? FindSpot(out string why)
        {
            why = "";
            float half = World.Size / 2f - 300f;
            int bad = TerrainTopology.MONUMENT | TerrainTopology.ROAD | TerrainTopology.ROADSIDE | TerrainTopology.RIVER | TerrainTopology.RIVERSIDE |
                      TerrainTopology.LAKE | TerrainTopology.LAKESIDE | TerrainTopology.BUILDING | TerrainTopology.CLIFF | TerrainTopology.BEACH |
                      TerrainTopology.OCEANSIDE | TerrainTopology.RAIL | TerrainTopology.RAILSIDE;
            var mons = TerrainMeta.Path.Monuments.Where(m => m != null).Select(m => m.transform.position).ToList();
            Vector3? best = null; float bestY = float.MinValue; int checkedN = 0;
            for (float x = -half; x <= half; x += 20f)
                for (float z = -half; z <= half; z += 20f)
                {
                    var p = new Vector3(x, 0f, z); float h = TerrainMeta.HeightMap.GetHeight(p); p.y = h;
                    if (h < bestY || h < TerrainMeta.WaterMap.GetHeight(p) + 6f) continue;
                    if ((TerrainMeta.TopologyMap.GetTopology(p, 12f) & bad) != 0) continue;
                    if (mons.Any(m => (new Vector2(m.x - x, m.z - z)).sqrMagnitude < 170f * 170f)) continue;
                    float lo = h, hi = h;
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; float y = TerrainMeta.HeightMap.GetHeight(new Vector3(x + Mathf.Cos(a) * 4.5f, 0f, z + Mathf.Sin(a) * 4.5f)); lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
                    if (hi - lo > 1.2f) continue;
                    checkedN++;
                    if (Physics.CheckSphere(p + Vector3.up * 3f, 5f, LayerMask.GetMask("World", "Tree", "Default"), QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.CheckSphere(p, 35f, LayerMask.GetMask("Construction", "Deployed"), QueryTriggerInteraction.Ignore)) continue;
                    best = p; bestY = h;
                }
            why = best.HasValue ? $"highest clear spot of {checkedN} flat candidates" : "no clear flat spot found";
            return best;
        }

        // ---------- building ----------
        private BaseEntity Spawn(string prefab, Vector3 pos, Quaternion rot, uint bid)
        {
            if (!StringPool.toNumber.ContainsKey(prefab)) { PrintWarning("missing prefab " + prefab); return null; }
            var e = GameManager.server.CreateEntity(prefab, pos, rot);
            if (e == null) return null;
            e.OwnerID = Marker;
            var b = e as BuildingBlock;
            if (b != null) { b.blockDefinition = PrefabAttribute.server.Find<Construction>(b.prefabID); b.grounded = true; }
            e.Spawn();
            if (b != null)
            {
                try { b.AttachToBuilding(bid); } catch { }
                b.SetGrade(BuildingGrade.Enum.Stone); b.SetHealthToMax(); b.grounded = true; b.SendNetworkUpdate();
            }
            parts.Add(e);
            return e;
        }

        private string Build(Vector3? at)
        {
            Remove();
            string why = "set by admin";
            var spot = at ?? FindSpot(out why);
            if (!spot.HasValue) return "Miles' Lookout: " + why + " - use /milestower here to place it.";
            var c = spot.Value;
            float top = c.y;
            for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; top = Mathf.Max(top, TerrainMeta.HeightMap.GetHeight(new Vector3(c.x + Mathf.Cos(a) * 4.5f, 0f, c.z + Mathf.Sin(a) * 4.5f))); }
            top += 0.3f;
            centre = new Vector3(c.x, top, c.z);
            uint bid = BuildingManager.server.NewBuildingID();
            var sq = new[] { new Vector2(-1.5f, -1.5f), new Vector2(1.5f, -1.5f), new Vector2(1.5f, 1.5f), new Vector2(-1.5f, 1.5f) };   // A stairs, B, C, D
            Func<Vector2, float, Vector3> at3 = (o, y) => new Vector3(c.x + o.x, y, c.z + o.y);
            foreach (var s in sq) Spawn(PFound, at3(s, top), Quaternion.identity, bid);
            for (int lv = 0; lv < Levels; lv++)
            {
                float y = top + lv * LevelH;
                Spawn(PStairs, at3(sq[0], y), Quaternion.identity, bid);
                for (int k = 1; k < 4; k++) Spawn(PFloor, at3(sq[k], y + LevelH), Quaternion.identity, bid);
                // the 8 outer edges
                foreach (var s in sq)
                {
                    var xe = new Vector2(s.x * 2f, s.y); var ze = new Vector2(s.x, s.y * 2f);
                    bool door = lv == 0 && s == sq[0];
                    string pxe = door ? PDoorway : (lv > 0 && (lv + (s.y > 0 ? 1 : 0)) % 2 == 1 ? PWindow : PWall);
                    string pze = lv > 0 && (lv + (s.x > 0 ? 1 : 0)) % 2 == 0 ? PWindow : PWall;
                    Spawn(pxe, at3(xe, y), Quaternion.identity, bid);                 // 1.0.1: walls on x-edges face along x
                    Spawn(pze, at3(ze, y), Quaternion.Euler(0f, 90f, 0f), bid);
                }
            }
            float roof = top + Levels * LevelH;
            foreach (var s in sq)
            {
                Spawn(PLow, at3(new Vector2(s.x * 2f, s.y), roof), Quaternion.identity, bid);
                Spawn(PLow, at3(new Vector2(s.x, s.y * 2f), roof), Quaternion.Euler(0f, 90f, 0f), bid);
            }
            // the beacon and lanterns on the open top
            var fire = Spawn(PFire, at3(sq[2], roof + 0.05f), Quaternion.identity, bid);
            Spawn(PLantern, at3(sq[1] + new Vector2(0.8f, -0.8f), roof + 0.05f), Quaternion.identity, bid);
            Spawn(PLantern, at3(sq[3] + new Vector2(-0.8f, 0.8f), roof + 0.05f), Quaternion.identity, bid);
            // plaques: one outside the door, one on the top
            var face = data.SignsFlipped ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.Euler(0f, -90f, 0f);
            var gb = new Vector3(c.x - 6.5f, 0f, c.z - 1.5f); gb.y = TerrainMeta.HeightMap.GetHeight(gb);
            Plaque(Spawn(PSign, gb, face, bid));
            Plaque(Spawn(PSign, at3(sq[3], roof + 0.05f), data.SignsFlipped ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity, bid));
            var mk = GameManager.server.CreateEntity(PMarker, centre) as MapMarkerGenericRadius;
            if (mk != null) { mk.OwnerID = Marker; mk.alpha = 0.7f; mk.color1 = new Color(0.91f, 0.77f, 0.41f); mk.color2 = Color.black; mk.radius = 0.25f; mk.Spawn(); mk.SendUpdate(); parts.Add(mk); }
            KeepLit();
            built = true;
            data.Spots[MapKey] = new[] { centre.x, centre.y, centre.z };
            Interface.Oxide.DataFileSystem.WriteObject("EchoMilesTower", data);
            return $"Miles' Lookout built at {centre} ({why}), {parts.Count} pieces, {Levels * LevelH:0} m tall.";
        }

        private void Plaque(BaseEntity e)
        {
            var s = e as Signage; if (s == null) return;
            try
            {
                var bytes = Convert.FromBase64String(PlaqueB64);
                if (s.textureIDs == null || s.textureIDs.Length == 0) return;
                s.textureIDs[0] = FileStorage.server.Store(bytes, FileStorage.Type.png, s.net.ID);
                using (var f = s.StartSetFlags(BaseEntity.FlagsUpdateMode.SendNetworkUpdate)) f.Set(BaseEntity.Flags.Locked, true);
                s.SendNetworkUpdate();
            }
            catch (Exception ex) { PrintWarning("plaque: " + ex.Message); }
        }

        private void KeepLit()
        {
            foreach (var e in parts.ToList())
            {
                var o = e as BaseOven; if (o == null || o.IsDestroyed) continue;
                var fuel = o.ShortPrefabName.Contains("lantern") ? "lowgradefuel" : "wood";
                if (o.inventory != null && o.inventory.GetAmount(ItemManager.FindItemDefinition(fuel).itemid, true) < 50)
                { var it = ItemManager.CreateByName(fuel, 200); if (it != null && !it.MoveToContainer(o.inventory)) it.Remove(); }
                if (!o.IsOn()) o.StartCooking();
            }
        }

        private void Remove()
        {
            Collect();
            foreach (var e in parts.ToList()) if (e != null && !e.IsDestroyed) e.Kill();
            parts.Clear(); built = false;
        }

        // ---------- protection ----------
        private object OnEntityTakeDamage(BaseCombatEntity e, HitInfo info)
        {
            if (e == null || info == null) return null;
            if (IsTower(e)) { info.damageTypes.ScaleAll(0f); return true; }
            var v = e as BasePlayer; var a = info.InitiatorPlayer;
            if (v != null && a != null && a != v && !v.IsNpc && !a.IsNpc && built && (NearTower(v.transform.position, PeaceRadius) || NearTower(a.transform.position, PeaceRadius)))
            { info.damageTypes.ScaleAll(0f); return true; }
            return null;
        }
        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            var p = planner?.GetOwnerPlayer(); if (p == null || !built) return null;
            var pos = target.entity != null ? target.entity.transform.position : target.position;
            if (NearTower(pos, NoBuildRadius) || NearTower(p.transform.position, NoBuildRadius)) { p.ChatMessage("<color=#E8C468>Miles' Lookout</color>: no building near the tower."); return false; }
            return null;
        }
        private object CanLootEntity(BasePlayer p, StorageContainer c) => IsTower(c) ? (object)false : null;
        private object CanPickupEntity(BasePlayer p, BaseEntity e) => IsTower(e) ? (object)false : null;
        private object OnStructureUpgrade(BaseCombatEntity e, BasePlayer p, BuildingGrade.Enum g) => IsTower(e) ? (object)true : null;
        private object OnStructureDemolish(BaseCombatEntity e, BasePlayer p, bool immediate) => IsTower(e) ? (object)true : null;
        private object OnStructureRotate(BaseCombatEntity e, BasePlayer p) => IsTower(e) ? (object)true : null;
        private object CanUpdateSign(BasePlayer p, Signage s) => IsTower(s) ? (object)false : null;
        private object OnOvenToggle(BaseOven o, BasePlayer p) => IsTower(o) ? (object)true : null;

        // ---------- commands ----------
        [ConsoleCommand("milestower.status")] private void CStatus(ConsoleSystem.Arg a) { if (!Admin(a)) return; Collect(); a.ReplyWith(parts.Count > 0 ? $"Miles' Lookout: {parts.Count} pieces at {centre} on {MapKey}" : "Miles' Lookout: not built on this map"); }
        [ConsoleCommand("milestower.build")] private void CBuild(ConsoleSystem.Arg a) { if (!Admin(a)) return; a.ReplyWith(Build(null)); }
        [ConsoleCommand("milestower.remove")] private void CRemove(ConsoleSystem.Arg a) { if (!Admin(a)) return; Remove(); data.Spots.Remove(MapKey); Interface.Oxide.DataFileSystem.WriteObject("EchoMilesTower", data); a.ReplyWith("Miles' Lookout removed from this map."); }
        [ConsoleCommand("milestower.flipsigns")] private void CFlip(ConsoleSystem.Arg a) { if (!Admin(a)) return; data.SignsFlipped = !data.SignsFlipped; float[] s; var at = data.Spots.TryGetValue(MapKey, out s) ? new Vector3(s[0], s[1] - 0.3f, s[2]) : (Vector3?)null; a.ReplyWith("Plaques turned round. " + Build(at)); }
        [ChatCommand("milestower")]
        private void ChTower(BasePlayer p, string cmd, string[] args)
        {
            if (!p.IsAdmin) return;
            if (args.Length > 0 && args[0] == "here") { var pos = p.transform.position; pos.y = TerrainMeta.HeightMap.GetHeight(pos); p.ChatMessage(Build(pos)); }
            else p.ChatMessage("/milestower here - rebuild Miles' Lookout where you stand");
        }
        private static bool Admin(ConsoleSystem.Arg a) => a.Connection == null || a.IsAdmin;
    }
}
